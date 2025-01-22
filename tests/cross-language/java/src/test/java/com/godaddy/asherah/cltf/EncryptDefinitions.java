package com.godaddy.asherah.cltf;

import com.godaddy.asherah.appencryption.Session;
import com.godaddy.asherah.appencryption.SessionFactory;
import com.godaddy.asherah.appencryption.kms.KeyManagementService;
import com.godaddy.asherah.appencryption.kms.StaticKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.persistence.JdbcMetastoreImpl;
import com.godaddy.asherah.crypto.BasicExpiringCryptoPolicy;
import com.godaddy.asherah.crypto.CryptoPolicy;
import com.zaxxer.hikari.HikariDataSource;
import io.cucumber.java.en.Given;
import io.cucumber.java.en.Then;
import io.cucumber.java.en.When;

import java.io.FileWriter;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.Base64;

import static com.godaddy.asherah.cltf.Constants.*;
import static org.junit.jupiter.api.Assertions.*;

public class EncryptDefinitions {
  private static String payloadString;
  private static String encryptedPayloadString;

  @Given("I have {string}")
  public void iHave(final String payload) {
    payloadString = payload;
  }

  @When("I encrypt the data")
  public void iEncryptTheData() {
    CryptoPolicy cryptoPolicy = BasicExpiringCryptoPolicy
        .newBuilder()
        .withKeyExpirationDays(KeyExpiryDays)
        .withRevokeCheckMinutes(RevokeCheckMinutes)
        .build();

    KeyManagementService keyManagementService = new StaticKeyManagementServiceImpl(KeyManagementStaticMasterKey);

    HikariDataSource dataSource = new HikariDataSource();
    dataSource.setJdbcUrl(JdbcConnectionString);
    JdbcMetastoreImpl metastore = JdbcMetastoreImpl.newBuilder(dataSource).build();

    // Create a session for the test
    try (SessionFactory sessionFactory = SessionFactory
        .newBuilder(DefaultProductId, DefaultServiceId)
        .withMetastore(metastore)
        .withCryptoPolicy(cryptoPolicy)
        .withKeyManagementService(keyManagementService)
        .build()) {

      // Now create an actual session for a partition (which in our case is a dummy id). This session is used
      // for a transaction and is closed automatically after use due to the AutoCloseable implementation.
      try (Session<byte[], byte[]> sessionBytes = sessionFactory.getSessionBytes(Constants.DefaultPartitionId)) {
        byte[] encryptedBytes = sessionBytes.encrypt(payloadString.getBytes(StandardCharsets.UTF_8));
        encryptedPayloadString = Base64.getEncoder().encodeToString(encryptedBytes);
      }
    }
  }

  @Then("I should get encrypted_data")
  public void iShouldGetEncryptedData() throws IOException {
    String tempFile = FileDirectory + FileName;
    // Delete any existing encrypted payload file
    Files.deleteIfExists(Paths.get(tempFile));

    // Write the encrypted payload to a file so that we can decrypt later
    FileWriter myWriter = new FileWriter(tempFile);
    myWriter.write(encryptedPayloadString);
    myWriter.close();
  }

  @Then("encrypted_data should not be equal to data")
  public void encryptedDataShouldNotBeEqualToData() {
    assertNotEquals(payloadString, encryptedPayloadString);
  }
}
