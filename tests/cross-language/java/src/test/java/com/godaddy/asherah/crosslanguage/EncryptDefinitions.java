package com.godaddy.asherah.crosslanguage;

import com.godaddy.asherah.appencryption.Session;
import com.godaddy.asherah.appencryption.SessionFactory;
import com.godaddy.asherah.appencryption.kms.StaticKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.persistence.JdbcMetastoreImpl;
import com.godaddy.asherah.crypto.CryptoPolicy;
import com.godaddy.asherah.crypto.NeverExpiredCryptoPolicy;
import com.zaxxer.hikari.HikariDataSource;
import io.cucumber.java.en.Given;
import io.cucumber.java.en.When;
import io.cucumber.java.en.Then;

import java.io.FileWriter;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.Base64;
import static org.junit.Assert.assertNotEquals;


public class EncryptDefinitions {
  private static String payloadString;
  private static byte[] encryptedData;


  @Given("I have {string}")
  public void i_have(String string) {
    // Write code here that turns the phrase above into concrete actions
    this.payloadString = string;
  }

  @When("I encrypt the data")
  public void i_encrypt_the_data() {

    CryptoPolicy cryptoPolicy = new NeverExpiredCryptoPolicy();
    HikariDataSource dataSource = new HikariDataSource();
    dataSource.setJdbcUrl("jdbc:mysql://localhost/test");
    dataSource.setUsername("root");
    dataSource.setPassword("Password123");
    JdbcMetastoreImpl metastore = JdbcMetastoreImpl.newBuilder(dataSource).build();
    // Write code here that turns the phrase above into concrete actions
    try (SessionFactory sessionFactory = SessionFactory
      .newBuilder("productId", "reference_app")
      .withMetastore(metastore)
      .withCryptoPolicy(cryptoPolicy)
      .withKeyManagementService(new StaticKeyManagementServiceImpl("mysupersecretstaticmasterkey!!!!"))
      .withMetricsEnabled()
      .build())
    {

      // Now create an actual session for a partition (which in our case is a pretend shopper id). This session is used
      // for a transaction and is closed automatically after use due to the AutoCloseable implementation.
      try (Session<byte[], byte[]> sessionBytes = sessionFactory
        .getSessionBytes("shopper123"))
      {
        encryptedData = sessionBytes.encrypt(this.payloadString.getBytes(StandardCharsets.UTF_8));
      }
    }
  }

  @Then("I get should get encrypted_data")
  public void i_get_should_get_encrypted_data() throws IOException {
    // Write code here that turns the phrase above into concrete actions
    FileWriter myWriter = new FileWriter("java_encrypted");
    myWriter.write(Base64.getEncoder().encodeToString(encryptedData));
    myWriter.close();
    System.out.println("File written");
  }

  @Then("encrypted_data should not equal data")
  public void encrypted_data_should_not_equal_data() {
    // Write code here that turns the phrase above into concrete actions
    assertNotEquals(payloadString, Base64.getEncoder().encodeToString(encryptedData));
  }
}
