package com.godaddy.asherah.appencryption.kms;

import com.amazonaws.SdkBaseException;
import com.amazonaws.services.kms.AWSKMS;
import com.amazonaws.services.kms.model.DecryptRequest;
import com.amazonaws.services.kms.model.EncryptRequest;
import com.amazonaws.services.kms.model.EncryptResult;
import com.amazonaws.services.kms.model.GenerateDataKeyRequest;
import com.amazonaws.services.kms.model.GenerateDataKeyResult;
import com.godaddy.asherah.appencryption.exceptions.AppEncryptionException;
import com.godaddy.asherah.appencryption.exceptions.KmsException;
import com.godaddy.asherah.appencryption.utils.Json;
import com.godaddy.asherah.appencryption.utils.MetricsUtil;
import com.godaddy.asherah.crypto.engine.bouncycastle.BouncyAes256GcmCrypto;
import com.godaddy.asherah.crypto.envelope.AeadEnvelopeCrypto;
import com.godaddy.asherah.crypto.keys.CryptoKey;
import com.google.common.util.concurrent.ThreadFactoryBuilder;

import io.micrometer.core.instrument.Metrics;
import io.micrometer.core.instrument.Timer;

import org.json.JSONArray;
import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import static com.godaddy.asherah.crypto.bufferutils.ManagedBufferUtils.wipeByteArray;

import java.nio.ByteBuffer;
import java.time.Instant;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.CancellationException;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.function.Supplier;
import java.util.stream.Collectors;
import java.util.stream.StreamSupport;

/**
 * Uses the AWS Key Management Service to provide an implementation of {@link KeyManagementService}. It provides
 * multi-region support, i.e. you can encrypt data in one region and decrypt it using the keys from another region.
 * The message format is:
 *   {
 *    "encryptedKey": "base64_encoded_bytes",
 *    "kmsKeks": [
 *      {
 *        "region": "aws_region",
 *        "arn": "arn",
 *        "encryptedKek": "base64_encoded_bytes"
 *      },
 *      ...
 *    ]
 *  }
 */
public class AwsKeyManagementServiceImpl implements KeyManagementService {
  private static final Logger logger = LoggerFactory.getLogger(AwsKeyManagementServiceImpl.class);

  static final String ENCRYPTED_KEY = "encryptedKey";
  static final String KMS_KEKS_KEY = "kmsKeks";

  static final String REGION_KEY = "region";
  static final String ARN_KEY = "arn";
  static final String ENCRYPTED_KEK = "encryptedKek";

  private final Timer encryptkeyTimer = Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".kms.aws.encryptkey");
  private final Timer decryptkeyTimer = Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".kms.aws.decryptkey");

  private final String preferredRegion;
  private final AeadEnvelopeCrypto crypto;
  private final AwsKmsClientFactory awsKmsClientFactory; // leaving here in case we later decide to create dynamically

  private final Map<String, AwsKmsArnClient> regionToArnAndClientMap = new LinkedHashMap<>();
  private final Comparator<String> regionPriorityComparator;

  Map<String, AwsKmsArnClient> getRegionToArnAndClientMap() {
    return regionToArnAndClientMap;
  }

  AwsKeyManagementServiceImpl(final Map<String, String> regionToArnMap, final String preferredRegion,
                              final AeadEnvelopeCrypto crypto, final AwsKmsClientFactory awsKmsClientFactory) {
    this.preferredRegion = preferredRegion;
    this.crypto = crypto;
    this.awsKmsClientFactory = awsKmsClientFactory;

    regionPriorityComparator = (String region1, String region2) -> {
      // Give preferred region top priority and fall back to remaining priority
      if (region1.equals(this.preferredRegion)) {
        return -1;
      }
      else if (region2.equals(this.preferredRegion)) {
        return 1;
      }
      else {
        // Treat them as equal for now
        // TODO consider adding logic to prefer geo/adjacent regions
        return 0;
      }
    };

    regionToArnMap.entrySet().stream()
        .sorted((regionToArn1, regionToArn2) ->
            regionPriorityComparator.compare(regionToArn1.getKey(), regionToArn2.getKey()))
        .forEach(regionToArn -> regionToArnAndClientMap.put(regionToArn.getKey(),
            new AwsKmsArnClient(regionToArn.getValue(),
                this.awsKmsClientFactory.createAwsKmsClient(regionToArn.getKey()))));
  }

  /**
   * Initialize a new builder for {@code AwsKeyManagementServiceImpl}.
   *
   * @param regionToArnMap A mapping of region:arn for the AWS KMS keys.
   * @param region The preferred region for AWS KMS.
   * @return The current {@code Builder} instance with initialized {@code regionToArnMap} and {@code region}.
   */
  public static Builder newBuilder(final Map<String, String> regionToArnMap, final String region) {
    return new Builder(regionToArnMap, region);
  }

  @Override
  public byte[] encryptKey(final CryptoKey key) {
    return encryptkeyTimer.record(() -> {
      Json kmsKeyEnvelope = new Json();

      // We generate a KMS datakey (plaintext and encrypted) and encrypt its plaintext key against remaining regions.
      // This allows us to be able to decrypt from any of the regions locally later.

      GenerateDataKeyResult dataKey = generateDataKey(this.regionToArnAndClientMap);
      byte[] dataKeyPlainText = dataKey.getPlaintext().array();

      // Using thread pool just for lifetime of method. Keys should be cached anyway. Use daemons so we don't block
      // JVM shutdown
      ExecutorService executorService = Executors.newFixedThreadPool(this.regionToArnAndClientMap.size(),
          new ThreadFactoryBuilder().setDaemon(true).build());
      try {
        byte[] encryptedKey = crypto.encryptKey(key, crypto.generateKeyFromBytes(dataKeyPlainText));
        kmsKeyEnvelope.put(ENCRYPTED_KEY, encryptedKey);

        List<Supplier<Optional<JSONObject>>> tasks = new ArrayList<>();
        regionToArnAndClientMap.forEach((region, arnAndClient) ->
            tasks.add(() -> {
              // If the ARN is different than the datakey's, call encrypt since it's another region
              if (!arnAndClient.arn.equals(dataKey.getKeyId())) {
                return encryptKeyAndBuildResult(arnAndClient.awsKmsClient, region, arnAndClient.arn, dataKeyPlainText);
              }
              else {
                // This is the datakey, so build kmsKey json for it
                return Optional.of(buildKmsRegionKeyJson(region, dataKey.getKeyId(),
                    dataKey.getCiphertextBlob().array()));
              }
            })
        );

        // Kickoff the tasks
        List<CompletableFuture<Optional<JSONObject>>> futures = tasks.stream()
              .map(task -> CompletableFuture.supplyAsync(task, executorService))
              .collect(Collectors.toList());
        // Collect results
        List<JSONObject> kmsRegionKeyJsonList = futures.stream()
            .map(CompletableFuture::join)
            .filter(Optional::isPresent)
            .map(Optional::get)
            .collect(Collectors.toList());
        // TODO Consider adding minimum or quorum check on number of entries
        kmsKeyEnvelope.put(KMS_KEKS_KEY, kmsRegionKeyJsonList);
      }
      catch (CompletionException | CancellationException e) {
        logger.error("Unexpected execution exception while encrypting KMS data key", e);
        throw new AppEncryptionException("unexpected execution error during encrypt", e);
      }
      finally {
        wipeByteArray(dataKeyPlainText);
        // We should be able to get away with this since AWS SDK should time out on its own and we're using daemons
        executorService.shutdown();
      }

      return kmsKeyEnvelope.toUtf8();
    });
  }

  Optional<JSONObject> encryptKeyAndBuildResult(final AWSKMS kmsClient, final String region, final String arn,
      final byte[] dataKeyPlainText) {
    try {
      Timer encryptTimer = Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".kms.aws.encrypt." + region);
      return encryptTimer.record(() -> {
        // Note we can't wipe plaintext key till end of calling method since underlying buffer shared by all requests
        EncryptRequest encryptRequest = new EncryptRequest()
            .withKeyId(arn)
            .withPlaintext(ByteBuffer.wrap(dataKeyPlainText));
        EncryptResult encryptResult = kmsClient.encrypt(encryptRequest);

        byte[] encryptedKeyEncryptionKey = encryptResult.getCiphertextBlob().array();

        return Optional.of(buildKmsRegionKeyJson(region, arn, encryptedKeyEncryptionKey));
      });
    }
    catch (SdkBaseException e) {
      logger.warn("Failed to encrypt generated data key via region {} KMS", region, e);
      // TODO Consider adding notification/CW alert
      return Optional.empty();
    }
  }

  /**
   * Attempt to generate a KMS datakey using the first successful response using a sorted map of available KMS clients.
   *
   * @param sortedRegionToArnAndClient A sorted dictionary mapping regions and their arns and kms clients.
   * @return A {@link GenerateDataKeyResult} object that contains the plain text key and the ciphertext for that key.
   * @exception KmsException Throws a {@link KmsException} if we're unable to generate a datakey in any AWS region.
   */
  GenerateDataKeyResult generateDataKey(final Map<String, AwsKmsArnClient> sortedRegionToArnAndClient) {
    for (Map.Entry<String, AwsKmsArnClient> regionToArnAndClient : sortedRegionToArnAndClient.entrySet()) {
      try {
        Timer generateDataKeyTimer =
            Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".kms.aws.generatedatakey." +
                regionToArnAndClient.getKey());
        return generateDataKeyTimer.record(() -> {
          AWSKMS kmsClient = regionToArnAndClient.getValue().awsKmsClient;
          GenerateDataKeyRequest dataKeyRequest = new GenerateDataKeyRequest()
              .withKeyId(regionToArnAndClient.getValue().arn)
              .withKeySpec("AES_256");
          return kmsClient.generateDataKey(dataKeyRequest);
        });
      }
      catch (SdkBaseException e) {
        logger.warn("Failed to generate data key via region {}, trying next region", regionToArnAndClient.getKey(), e);
        // TODO Consider adding notification/CW alert
      }
    }

    throw new KmsException("could not successfully generate data key using any regions");
  }

  private JSONObject buildKmsRegionKeyJson(final String region, final String arn,
      final byte[] encryptedKeyEncryptionKey) {
    Json kmsRegionKeyJson = new Json();

    // NOTE: ARN not needed in decrypt, but storing for now in case we want to later use for encryption context,
    // policy, etc.
    kmsRegionKeyJson.put(REGION_KEY, region);
    kmsRegionKeyJson.put(ARN_KEY, arn);
    kmsRegionKeyJson.put(ENCRYPTED_KEK, encryptedKeyEncryptionKey);

    return kmsRegionKeyJson.toJsonObject();
  }

  @Override
  public CryptoKey decryptKey(final byte[] keyCipherText, final Instant keyCreated, final boolean revoked) {
    return decryptkeyTimer.record(() -> {
      Json kmsKeyEnvelope = new Json(keyCipherText);

      byte[] encryptedKey = kmsKeyEnvelope.getBytes(ENCRYPTED_KEY);

      for (Json kmsRegionKeyJson : getPrioritizedKmsRegionKeyJsonList(kmsKeyEnvelope.getJSONArray(KMS_KEKS_KEY))) {
        String region = kmsRegionKeyJson.getString(REGION_KEY);

        AwsKmsArnClient arnAndClient = regionToArnAndClientMap.get(region);
        if (arnAndClient == null) {
          logger.warn("Failed to decrypt due to no client for region {}, trying next region", region);
          continue;
        }
        byte[] kmsKeyEncryptionKey = kmsRegionKeyJson.getBytes(ENCRYPTED_KEK);

        try {
          Timer decryptTimer = Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".kms.aws.decrypt." + region);
          return decryptTimer.record(() ->
            decryptKmsEncryptedKey(arnAndClient.awsKmsClient, encryptedKey, keyCreated, kmsKeyEncryptionKey, revoked)
          );
        }
        catch (SdkBaseException e) {
          logger.warn("Failed to decrypt via region {} KMS, trying next region", region, e);
          // TODO Consider adding notification/CW alert
        }
      }

      throw new KmsException("could not successfully decrypt key using any regions");
    });
  }

  /**
   * Gets an ordered list of KMS region key json objects to use. Uses preferred region and falls back to others as
   * appropriate.
   *
   * @param kmsRegionKeyArray A non-prioritized array of KMS region key objects.
   * @return A list of KMS region key json objects, prioritized by regions.
   */
  List<Json> getPrioritizedKmsRegionKeyJsonList(final JSONArray kmsRegionKeyArray) {
    return StreamSupport.stream(kmsRegionKeyArray.spliterator(), false)
        .map(object -> new Json((JSONObject) object))
        .sorted((kmsRegionKeyJson1, kmsRegionKeyJson2) ->
            regionPriorityComparator.compare(kmsRegionKeyJson1.getString(REGION_KEY),
                kmsRegionKeyJson2.getString(REGION_KEY)))
        .collect(Collectors.toList());
  }

  CryptoKey decryptKmsEncryptedKey(final AWSKMS awsKmsClient, final byte[] cipherText, final Instant keyCreated,
                                   final byte[] kmsKeyEncryptionKey, final boolean revoked) {
    DecryptRequest decryptRequest = new DecryptRequest()
        .withCiphertextBlob(ByteBuffer.wrap(kmsKeyEncryptionKey));
    byte[] plaintextBackingBytes = awsKmsClient.decrypt(decryptRequest).getPlaintext().array();
    try {
      return crypto.decryptKey(cipherText, keyCreated, crypto.generateKeyFromBytes(plaintextBackingBytes), revoked);
    }
    finally {
      wipeByteArray(plaintextBackingBytes);
    }
  }

  static final class AwsKmsArnClient {
    private final String arn;
    private final AWSKMS awsKmsClient;

    private AwsKmsArnClient(final String arn, final AWSKMS awsKmsClient) {
      this.arn = arn;
      this.awsKmsClient = awsKmsClient;
    }
  }

  public static final class Builder {

    private final Map<String, String> regionToArnMap;
    private final String preferredRegion;

    private Builder(final Map<String, String> regionToArnMap, final String region) {
      this.regionToArnMap = regionToArnMap;
      this.preferredRegion = region;
    }

    /**
     * Builds the {@link AwsKeyManagementServiceImpl} object.
     *
     * @return The fully instantiated {@link AwsKeyManagementServiceImpl} object.
     */
    public AwsKeyManagementServiceImpl build() {
      return new AwsKeyManagementServiceImpl(regionToArnMap, preferredRegion, new BouncyAes256GcmCrypto(),
          new AwsKmsClientFactory());
    }
  }
}
