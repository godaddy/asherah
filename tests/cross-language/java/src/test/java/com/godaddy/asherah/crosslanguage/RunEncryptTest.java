package com.godaddy.asherah.crosslanguage;

import io.cucumber.junit.Cucumber;
import io.cucumber.junit.CucumberOptions;
import org.junit.runner.RunWith;

@RunWith(Cucumber.class)
@CucumberOptions(
  plugin = {"pretty"},
  features = "../features/encrypt.feature",
  monochrome = true,
  strict = true
)
public class RunEncryptTest {
}
