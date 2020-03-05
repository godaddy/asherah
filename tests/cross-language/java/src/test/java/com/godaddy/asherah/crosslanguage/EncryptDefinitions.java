package com.godaddy.asherah.crosslanguage;

import com.godaddy.asherah.appencryption.Session;
import com.godaddy.asherah.appencryption.SessionFactory;
import com.godaddy.asherah.appencryption.kms.KeyManagementService;
import com.godaddy.asherah.appencryption.kms.StaticKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.persistence.JdbcMetastoreImpl;
import com.godaddy.asherah.crypto.CryptoPolicy;
import com.godaddy.asherah.crypto.BasicExpiringCryptoPolicy;
import com.zaxxer.hikari.HikariDataSource;
import io.cucumber.java.en.Given;
import io.cucumber.java.en.When;
import io.cucumber.java.en.Then;

import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.Base64;
import static org.junit.Assert.assertNotEquals;

import static com.godaddy.asherah.crosslanguage.Constants.*;


public class EncryptDefinitions {
  private static String payloadString;
  private static String encryptedPayloadString;
  private static byte[] encryptedBytes;


  @Given("I have {string}")
  public void i_have(String string) {
    this.payloadString = string;
  }

  @When("I encrypt the data")
  public void i_encrypt_the_data() {

    CryptoPolicy cryptoPolicy = BasicExpiringCryptoPolicy
      .newBuilder()
      .withKeyExpirationDays(KeyExpiryDays)
      .withRevokeCheckMinutes(RevokeCheckMinutes)
      .build();

    KeyManagementService keyManagementService =
      new StaticKeyManagementServiceImpl(KeyManagementStaticMasterKey);

    HikariDataSource dataSource = new HikariDataSource();
    dataSource.setJdbcUrl(JdbcConnectionString);
    dataSource.setUsername(User);
    dataSource.setPassword(Password);
    JdbcMetastoreImpl metastore = JdbcMetastoreImpl.newBuilder(dataSource).build();

    // Create a session for the test
    try (SessionFactory sessionFactory = SessionFactory
      .newBuilder(DefaultProductId, DefaultServiceId)
      .withMetastore(metastore)
      .withCryptoPolicy(cryptoPolicy)
      .withKeyManagementService(keyManagementService)
      .build())
    {

      // Now create an actual session for a partition (which in our case is a dummy id). This session is used
      // for a transaction and is closed automatically after use due to the AutoCloseable implementation.
      try (Session<byte[], byte[]> sessionBytes = sessionFactory
        .getSessionBytes(Constants.DefaultPartitionId))
      {
        encryptedBytes = sessionBytes.encrypt(this.payloadString.getBytes(StandardCharsets.UTF_8));
        encryptedPayloadString = Base64.getEncoder().encodeToString(encryptedBytes);
      }
    }
  }

  @Then("I get should get encrypted_data")
  public void i_get_should_get_encrypted_data() throws IOException {
    // Write the encrypted payload to a file so that we can decrypt later
    String path = System.getProperty("user.dir") + File.separator + ".." + File.separator + FileDirectory + File.separator;
    FileWriter myWriter = new FileWriter(path + FileName);
    myWriter.write(encryptedPayloadString);
    myWriter.close();
  }

  @Then("encrypted_data should not equal data")
  public void encrypted_data_should_not_equal_data() {
    assertNotEquals(payloadString, encryptedPayloadString);
  }
}
