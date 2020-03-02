package com.godaddy.asherah.crosslanguage;

import com.godaddy.asherah.appencryption.Session;
import com.godaddy.asherah.appencryption.SessionFactory;
import com.godaddy.asherah.appencryption.kms.StaticKeyManagementServiceImpl;
import com.godaddy.asherah.appencryption.persistence.JdbcMetastoreImpl;
import com.godaddy.asherah.crypto.CryptoPolicy;
import com.godaddy.asherah.crypto.NeverExpiredCryptoPolicy;
import com.zaxxer.hikari.HikariDataSource;
import io.cucumber.java.en.Given;
import io.cucumber.java.en.Then;
import io.cucumber.java.en.When;

import java.io.File;
import java.io.FileNotFoundException;
import java.nio.charset.StandardCharsets;
import java.util.Base64;
import java.util.Scanner;

import static org.junit.Assert.assertEquals;


public class DecryptDefinitions {
  private static String decryptedPayloadString;
  private static String temp;


  @Given("I have encrypted_data from {string}")
  public void i_have_encrypted_data_from(String fileName) throws FileNotFoundException {
    // Write code here that turns the phrase above into concrete actions
    Scanner sc = new Scanner(new File("../encrypted_files/"+fileName));
    temp = sc.nextLine();
    sc.close();
  }

  @When("I decrypt the encrypted_data")
  public void i_decrypt_the_encrypted_data() {
    // Write code here that turns the phrase above into concrete actions
    CryptoPolicy cryptoPolicy = new NeverExpiredCryptoPolicy();
    HikariDataSource dataSource = new HikariDataSource();
    dataSource.setJdbcUrl("jdbc:mysql://127.0.0.1/test");
    dataSource.setUsername("root");
    dataSource.setPassword("Password123");
    JdbcMetastoreImpl metastore = JdbcMetastoreImpl.newBuilder(dataSource).build();
    try (SessionFactory sessionFactory = SessionFactory
      .newBuilder("productId", "reference_app")
      .withMetastore(metastore)
      .withCryptoPolicy(cryptoPolicy)
      .withKeyManagementService(new StaticKeyManagementServiceImpl("mysupersecretstaticmasterkey!!!!"))
      .withMetricsEnabled()
      .build())
    {
      try (Session<byte[], byte[]> sessionBytes = sessionFactory
        .getSessionBytes("shopper123"))
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
