package com.godaddy.asherah.cltf;

import io.cucumber.junit.Cucumber;
import io.cucumber.junit.CucumberOptions;
import org.junit.runner.RunWith;

@RunWith(Cucumber.class)
@CucumberOptions(
    plugin = {"pretty"},
    features = "../features/decrypt.feature",
    monochrome = true,
    strict = true
)
public class RunDecryptTest {
}
