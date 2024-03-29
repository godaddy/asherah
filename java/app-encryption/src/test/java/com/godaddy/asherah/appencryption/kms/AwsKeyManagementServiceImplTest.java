package com.godaddy.asherah.appencryption.kms;

import software.amazon.awssdk.core.SdkBytes;
import software.amazon.awssdk.core.exception.SdkException;
import software.amazon.awssdk.services.kms.KmsClient;
import software.amazon.awssdk.services.kms.model.*;
import com.godaddy.asherah.appencryption.exceptions.AppEncryptionException;
import com.godaddy.asherah.appencryption.exceptions.KmsException;
import com.godaddy.asherah.appencryption.utils.Json;
import com.godaddy.asherah.crypto.envelope.AeadEnvelopeCrypto;
import com.godaddy.asherah.crypto.keys.CryptoKey;
import com.google.common.collect.ImmutableMap;
import org.json.JSONArray;
import org.json.JSONObject;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.nio.ByteBuffer;
import java.time.Instant;
import java.util.*;

import static com.godaddy.asherah.appencryption.kms.AwsKeyManagementServiceImpl.*;
import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class AwsKeyManagementServiceImplTest {

  static final String US_EAST_1 = "us-east-1";
  static final String ARN_US_EAST_1 = "arn-us-east-1";
  static final String US_WEST_1 = "us-west-1";
  static final String ARN_US_WEST_1 = "arn-us-west-1";

  Map<String, String> regionToArnMap = ImmutableMap.of(
      US_EAST_1, ARN_US_EAST_1,
      US_WEST_1, ARN_US_WEST_1
  );
  String preferredRegion = US_WEST_1;

  @Mock
  KmsClient awsKmsClient;
  @Mock
  AeadEnvelopeCrypto crypto;
  @Mock
  AwsKmsClientFactory awsKmsClientFactory;
  @Mock
  CryptoKey cryptoKey;

  AwsKeyManagementServiceImpl awsKeyManagementServiceImpl;

  @BeforeEach
  void setUp() {
    // This will be fragile since it's used in the constructor itself. If unit tests need to mock different clients
    // being returned, they'll likely need to create their own new flavors of this mock and the main spy.
    when(awsKmsClientFactory.build(any())).thenReturn(awsKmsClient);

    awsKeyManagementServiceImpl =
        spy(new AwsKeyManagementServiceImpl(regionToArnMap, preferredRegion, crypto, awsKmsClientFactory));
  }

  @Test
  void testRegionToArnAndClientMapGeneration() {
    AwsKeyManagementServiceImpl awsKeyManagementService =
        new AwsKeyManagementServiceImpl(regionToArnMap, preferredRegion, crypto, awsKmsClientFactory);
    Map.Entry<String, AwsKmsArnClient> record =
        awsKeyManagementService.getRegionToArnAndClientMap().entrySet().iterator().next();
    assertEquals(preferredRegion, record.getKey());
    assertEquals(regionToArnMap.size(), awsKeyManagementService.getRegionToArnAndClientMap().size());
  }

  @Test
  void testDecryptKeySuccessful() {
    byte[] encryptedKey = new byte[]{0, 1};
    byte[] kmsKeyEncryptionKey = new byte[]{2, 3};
    JSONObject kmsKeyEnvelope = new JSONObject(ImmutableMap.of(
        ENCRYPTED_KEY, Base64.getEncoder().encodeToString(encryptedKey),
        KMS_KEKS_KEY, Arrays.asList(
            ImmutableMap.of(
                REGION_KEY, US_WEST_1,
                ARN_KEY, ARN_US_WEST_1,
                ENCRYPTED_KEK, Base64.getEncoder().encodeToString(kmsKeyEncryptionKey)
            )
        )
    ));
    Instant now = Instant.now();
    boolean revoked = false;
    doReturn(cryptoKey)
        .when(awsKeyManagementServiceImpl)
        .decryptKmsEncryptedKey(awsKmsClient, encryptedKey, now, kmsKeyEncryptionKey, revoked);

    CryptoKey actualCryptoKey = awsKeyManagementServiceImpl.decryptKey(new Json(kmsKeyEnvelope).toUtf8(), now, revoked);
    assertEquals(cryptoKey, actualCryptoKey);
  }

  @Test
  void testDecryptKeyWithMissingRegionInPayloadShouldSkipAndSucceed() {
    byte[] encryptedKey = new byte[]{0, 1};
    byte[] kmsKeyEncryptionKey = new byte[]{2, 3};
    JSONObject kmsKeyEnvelope = new JSONObject(ImmutableMap.of(
        ENCRYPTED_KEY, Base64.getEncoder().encodeToString(encryptedKey),
        KMS_KEKS_KEY, Arrays.asList(
            ImmutableMap.of(
                REGION_KEY, "a_region", // should appear before valid us-east region
                ARN_KEY, "a_arn",
                ENCRYPTED_KEK, Base64.getEncoder().encodeToString(kmsKeyEncryptionKey)
            ),
            ImmutableMap.of(
                REGION_KEY, US_EAST_1,
                ARN_KEY, ARN_US_EAST_1,
                ENCRYPTED_KEK, Base64.getEncoder().encodeToString(kmsKeyEncryptionKey)
            )
        )
    ));
    Instant now = Instant.now();
    boolean revoked = false;
    doReturn(cryptoKey)
        .when(awsKeyManagementServiceImpl)
        .decryptKmsEncryptedKey(awsKmsClient, encryptedKey, now, kmsKeyEncryptionKey, revoked);

    CryptoKey actualCryptoKey = awsKeyManagementServiceImpl.decryptKey(new Json(kmsKeyEnvelope).toUtf8(), now, revoked);
    assertEquals(cryptoKey, actualCryptoKey);
  }

  @Test
  void testDecryptKeyWithKmsFailureShouldThrowKmsException() {
    byte[] encryptedKey = new byte[]{0, 1};
    byte[] kmsKeyEncryptionKey = new byte[]{2, 3};
    JSONObject kmsKeyEnvelope = new JSONObject(ImmutableMap.of(
        ENCRYPTED_KEY, Base64.getEncoder().encodeToString(encryptedKey),
        KMS_KEKS_KEY, Arrays.asList(
            ImmutableMap.of(
                REGION_KEY, US_WEST_1,
                ARN_KEY, ARN_US_WEST_1,
                ENCRYPTED_KEK, Base64.getEncoder().encodeToString(kmsKeyEncryptionKey)
            )
        )
    ));
    // apparently not needed since we tell the call that uses it to fail anyway?
    //when(awsKmsClientFactory.createAwsKmsClient(any())).thenReturn(awsKmsClient);
    doThrow(SdkException.class)
        .when(awsKeyManagementServiceImpl).decryptKmsEncryptedKey(any(), any(), any(), any(), anyBoolean());

    assertThrows(KmsException.class,
        () -> awsKeyManagementServiceImpl.decryptKey(new Json(kmsKeyEnvelope).toUtf8(), Instant.now(), false));
  }

  @Test
  void testGetPrioritizedKmsRegionKeyJsonList() {
    JSONArray kmsRegionKeyArray = new JSONArray(Arrays.asList(
        // really only need regions for the method under test
        ImmutableMap.of(
            REGION_KEY, US_EAST_1
        ),
        ImmutableMap.of(
            REGION_KEY, "a" // should always be lexicographically first
        ),
        ImmutableMap.of(
            REGION_KEY, "zzzzzzzzzzzzzzzzzzzzzzzzz" // should always be lexicographically last
        ),
        ImmutableMap.of(
            REGION_KEY, preferredRegion
        )
    ));
    List<Json> ret = awsKeyManagementServiceImpl.getPrioritizedKmsRegionKeyJsonList(kmsRegionKeyArray);
    assertEquals(preferredRegion, ret.get(0).getString(REGION_KEY));
    // If we ever add geo awareness, add unit tests appropriately
  }

  @Test
  void testDecryptKmsEncryptedKeySuccessful() {
    byte[] cipherText = new byte[]{0, 1};
    Instant now = Instant.now();
    byte[] keyEncryptionKey = new byte[]{2, 3};
    boolean revoked = false;
    byte[] plaintextBackingBytes = new byte[]{4, 5};
    DecryptResponse decryptResponse = DecryptResponse.builder()
      .plaintext(SdkBytes.fromByteArrayUnsafe(plaintextBackingBytes))
      .build();
    when(awsKmsClient.decrypt(any(DecryptRequest.class))).thenReturn(decryptResponse);
    when(crypto.generateKeyFromBytes(plaintextBackingBytes)).thenReturn(cryptoKey);
    CryptoKey expectedKey = mock(CryptoKey.class);
    when(crypto.decryptKey(cipherText, now, cryptoKey, revoked)).thenReturn(expectedKey);

    CryptoKey actualKey =
        awsKeyManagementServiceImpl.decryptKmsEncryptedKey(awsKmsClient, cipherText, now, keyEncryptionKey, revoked);
    assertEquals(expectedKey, actualKey);
    assertArrayEquals(new byte[]{0, 0},  plaintextBackingBytes);
  }

  @Test
  void testDecryptKmsEncryptedKeyWithKmsFailureShouldThrowException() {
    byte[] cipherText = new byte[]{0, 1};
    byte[] keyEncryptionKey = new byte[]{2, 3};
    when(awsKmsClient.decrypt(any(DecryptRequest.class))).thenThrow(SdkException.class);

    assertThrows(SdkException.class,
        () -> awsKeyManagementServiceImpl
            .decryptKmsEncryptedKey(awsKmsClient, cipherText, Instant.now(), keyEncryptionKey, false));
  }

  @Test
  void testDecryptKmsEncryptedKeyWithCryptoFailureShouldThrowExceptionAndWipeBytes() {
    byte[] cipherText = new byte[]{0, 1};
    Instant now = Instant.now();
    byte[] keyEncryptionKey = new byte[]{2, 3};
    boolean revoked = false;
    byte[] plaintextBackingBytes = new byte[]{4, 5};
    DecryptResponse decryptResponse = DecryptResponse.builder()
      .plaintext(SdkBytes.fromByteArrayUnsafe(plaintextBackingBytes))
      .build();
    when(awsKmsClient.decrypt(any(DecryptRequest.class))).thenReturn(decryptResponse);
    when(crypto.generateKeyFromBytes(plaintextBackingBytes)).thenReturn(cryptoKey);
    when(crypto.decryptKey(cipherText, now, cryptoKey, revoked)).thenThrow(AppEncryptionException.class);

    assertThrows(AppEncryptionException.class,
        () -> awsKeyManagementServiceImpl
            .decryptKmsEncryptedKey(awsKmsClient, cipherText, now, keyEncryptionKey, revoked));
    assertArrayEquals(new byte[]{0, 0},  plaintextBackingBytes);
  }

  @Test
  void testPrimaryBuilderPath() {
    AwsKeyManagementServiceImpl.Builder awsKeyManagementServicePrimaryBuilder =
        AwsKeyManagementServiceImpl.newBuilder(regionToArnMap,
        preferredRegion);
    AwsKeyManagementServiceImpl awsKeyManagementServiceBuilder = awsKeyManagementServicePrimaryBuilder.build();
    assertNotNull(awsKeyManagementServiceBuilder);
  }

  @Test
  void testGenerateDataKeySuccessful() {
    Map<String, AwsKmsArnClient> sortedRegionToArnAndClient = awsKeyManagementServiceImpl.getRegionToArnAndClientMap();
    GenerateDataKeyRequest expectedRequest = GenerateDataKeyRequest.builder()
        .keyId(ARN_US_WEST_1) // preferred regions's ARN, verify it's the first and hence returned
        .keySpec("AES_256")
        .build();
    GenerateDataKeyResponse dataKeyResponse = mock(GenerateDataKeyResponse.class);

    when(awsKmsClient.generateDataKey(eq(expectedRequest))).thenReturn(dataKeyResponse);
    GenerateDataKeyResponse dataKeyResponseActual = awsKeyManagementServiceImpl.generateDataKey(sortedRegionToArnAndClient);
    assertEquals(dataKeyResponse, dataKeyResponseActual);
  }

  @Test
  void testGenerateDataKeyWithKmsFailureShouldThrowKmsException() {
    Map<String, AwsKmsArnClient> sortedRegionToArnAndClient = awsKeyManagementServiceImpl.getRegionToArnAndClientMap();
    when(awsKmsClient.generateDataKey(any(GenerateDataKeyRequest.class))).thenThrow(SdkException.class);

    assertThrows(KmsException.class,
        () -> awsKeyManagementServiceImpl.generateDataKey(sortedRegionToArnAndClient));
  }

  @Test
  void testEncryptKeyAndBuildResult() {
    byte[] encryptedKey = new byte[] {0,1};
    EncryptResponse encryptResponse = EncryptResponse.builder()
      .ciphertextBlob(SdkBytes.fromByteArrayUnsafe(encryptedKey))
      .build();

    when(awsKmsClient.encrypt(any(EncryptRequest.class))).thenReturn(encryptResponse);
    byte[] dataKeyPlainText = new byte[] {2,3};
    Optional<JSONObject> actualResult =
        awsKeyManagementServiceImpl
            .encryptKeyAndBuildResult(awsKmsClient, preferredRegion, ARN_US_WEST_1, dataKeyPlainText);
    assertEquals(preferredRegion, actualResult.get().get(REGION_KEY));
    assertEquals(ARN_US_WEST_1, actualResult.get().get(ARN_KEY));
    assertArrayEquals(encryptedKey, Base64.getDecoder().decode(actualResult.get().getString(ENCRYPTED_KEK)));
  }

  @Test
  void testEncryptKeyAndBuildResultReturnEmptyOptional() {
    byte[] dataKeyPlainText = new byte[] {0,1};
   when(awsKmsClient.encrypt(any(EncryptRequest.class))).thenThrow(SdkException.class);
    Optional<JSONObject> actualResult =
        awsKeyManagementServiceImpl
            .encryptKeyAndBuildResult(awsKmsClient, preferredRegion, ARN_US_WEST_1, dataKeyPlainText);
    assertEquals(Optional.empty(), actualResult);
  }

  @Test
  void testEncryptKeySuccessful() {
    byte[] encryptedKey = new byte[] {3,4};
    ByteBuffer dataKeyPlainText = ByteBuffer.wrap(new byte[] {1,2});
    ByteBuffer dataKeyCipherText = ByteBuffer.wrap(new byte[] {5,6});
    ByteBuffer encryptKeyCipherText = ByteBuffer.wrap(new byte[] {7,8});

    JSONObject encryptKeyAndBuildResultJson =  new JSONObject(ImmutableMap.of(
        REGION_KEY, US_EAST_1,
        ARN_KEY, ARN_US_EAST_1,
        ENCRYPTED_KEK, Base64.getEncoder().encodeToString(encryptKeyCipherText.array())
    ));

    JSONObject kmsKeyEnvelope = new JSONObject(ImmutableMap.of(
        KMS_KEKS_KEY, Arrays.asList(
            ImmutableMap.of(
                REGION_KEY, US_WEST_1,
                ARN_KEY, ARN_US_WEST_1,
                ENCRYPTED_KEK, Base64.getEncoder().encodeToString(dataKeyCipherText.array())
            ),
            encryptKeyAndBuildResultJson
        ),
        ENCRYPTED_KEY, Base64.getEncoder().encodeToString(encryptedKey)
    ));

    GenerateDataKeyResponse dataKeyMock = mock(GenerateDataKeyResponse.class);
    CryptoKey generatedDataKeyCryptoKey = mock(CryptoKey.class);

    when(awsKeyManagementServiceImpl.generateDataKey(awsKeyManagementServiceImpl.getRegionToArnAndClientMap()))
        .thenReturn(dataKeyMock);
    when(dataKeyMock.plaintext()).thenReturn(SdkBytes.fromByteArrayUnsafe(dataKeyPlainText.array()));
    when(crypto.generateKeyFromBytes(dataKeyPlainText.array())).thenReturn(generatedDataKeyCryptoKey);
    when(crypto.encryptKey(cryptoKey, generatedDataKeyCryptoKey)).thenReturn(encryptedKey);
    when(dataKeyMock.keyId()).thenReturn(ARN_US_WEST_1);
    doReturn(Optional.of(encryptKeyAndBuildResultJson)).when(awsKeyManagementServiceImpl)
        .encryptKeyAndBuildResult(eq(awsKmsClient), eq(US_EAST_1), eq(ARN_US_EAST_1), eq(dataKeyPlainText.array()));
    when(dataKeyMock.ciphertextBlob()).thenReturn(SdkBytes.fromByteArrayUnsafe(dataKeyCipherText.array()));

    byte[] encryptedResult = awsKeyManagementServiceImpl.encryptKey(cryptoKey);
    Json kmsKeyEnvelopeResult = new Json(encryptedResult);

    assertTrue(kmsKeyEnvelope.similar(kmsKeyEnvelopeResult.toJsonObject()));
    assertArrayEquals(new byte[]{0, 0},  dataKeyPlainText.array());
  }

  @Test
  void testEncryptKeyShouldThrowExceptionAndWipeBytes() {
    ByteBuffer dataKeyPlainText = ByteBuffer.wrap(new byte[] {1,2});
    byte[] encryptedKey = new byte[] {3,4};

    GenerateDataKeyResponse dataKeyMock = mock(GenerateDataKeyResponse.class);
    CryptoKey generatedDataKeyCryptoKey = mock(CryptoKey.class);

    when(awsKeyManagementServiceImpl.generateDataKey(awsKeyManagementServiceImpl.getRegionToArnAndClientMap()))
        .thenReturn(dataKeyMock);
    when(dataKeyMock.plaintext()).thenReturn(SdkBytes.fromByteArrayUnsafe(dataKeyPlainText.array()));
    when(crypto.generateKeyFromBytes(dataKeyPlainText.array())).thenReturn(generatedDataKeyCryptoKey);
    // Inject unexpected exception so the CompleteableFuture.join throws a CompletionException
    doThrow(RuntimeException.class).when(awsKeyManagementServiceImpl)
        .encryptKeyAndBuildResult(any(), any(), any(), any());
    when(crypto.encryptKey(cryptoKey, generatedDataKeyCryptoKey)).thenReturn(encryptedKey);

    assertThrows(AppEncryptionException.class, () -> awsKeyManagementServiceImpl.encryptKey(cryptoKey));
    assertArrayEquals(new byte[]{0, 0},  dataKeyPlainText.array());
  }

}
