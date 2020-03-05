package com.godaddy.asherah.crosslanguage;

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

import java.io.File;
import java.io.FileNotFoundException;
import java.nio.charset.StandardCharsets;
import java.util.Base64;
import java.util.Scanner;

import static com.godaddy.asherah.crosslanguage.Constants.*;
import static com.godaddy.asherah.crosslanguage.Constants.DefaultServiceId;
import static org.junit.Assert.assertEquals;


public class DecryptDefinitions {
  private static String decryptedPayloadString;
  private static String temp;


  @Given("I have encrypted_data from {string}")
  public void i_have_encrypted_data_from(String fileName) throws FileNotFoundException {
    // Write code here that turns the phrase above into concrete actions
    String path = System.getProperty("user.dir") + File.separator + ".." + File.separator + FileDirectory + File.separator;
    Scanner sc = new Scanner(new File(path + fileName));
    temp = sc.nextLine();
    sc.close();
  }

  @When("I decrypt the encrypted_data")
  public void i_decrypt_the_encrypted_data() {
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

    try (SessionFactory sessionFactory = SessionFactory
      .newBuilder(DefaultProductId, DefaultServiceId)
      .withMetastore(metastore)
      .withCryptoPolicy(cryptoPolicy)
      .withKeyManagementService(keyManagementService)
      .build())
    {

      // Now create an actual session for a partition (which in our case is a pretend shopper id). This session is used
      // for a transaction and is closed automatically after use due to the AutoCloseable implementation.
      try (Session<byte[], byte[]> sessionBytes = sessionFactory
        .getSessionBytes(Constants.DefaultPartitionId))
      {
        byte[] xx = Base64.getDecoder().decode(temp);
        decryptedPayloadString = new String(sessionBytes.decrypt(Base64.getDecoder().decode(temp)),
          StandardCharsets.UTF_8);
      }
    }
  }

  @Then("I get should get decrypted_data")
  public void i_get_should_get_decrypted_data() {
    // Write code here that turns the phrase above into concrete actions
    return;
  }
  @Then("decrypted_data should be equal to {string}")
  public void decrypted_data_should_be_equal_to(String string) {
    // Write code here that turns the phrase above into concrete actions
    assertEquals(string, decryptedPayloadString);
  }
}
