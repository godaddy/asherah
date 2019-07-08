package com.godaddy.asherah.testapp;

import com.godaddy.asherah.testapp.configuration.ServerConfiguration;
import com.godaddy.asherah.testapp.rest.AppEncryptionResources;
import com.godaddy.asherah.testapp.rest.SecureMemoryResources;

import io.dropwizard.Application;
import io.dropwizard.configuration.EnvironmentVariableSubstitutor;
import io.dropwizard.configuration.ResourceConfigurationSourceProvider;
import io.dropwizard.configuration.SubstitutingSourceProvider;
import io.dropwizard.setup.Bootstrap;
import io.dropwizard.setup.Environment;

public class TestAppRestMain extends Application<ServerConfiguration> {

  public static void main(final String[] args) throws Exception {
    new TestAppRestMain().run(args);
  }

  @Override
  public void run(final ServerConfiguration configuration, final Environment environment) {
    // Run one-time init before anything
    TestSetup.init(configuration);

    // Bind all resources
    AppEncryptionResources appEncryptionResources = new AppEncryptionResources(configuration);
    SecureMemoryResources secureMemoryResources = new SecureMemoryResources(configuration);
    environment.jersey().register(appEncryptionResources);
    environment.jersey().register(secureMemoryResources);
  }

  @Override
  public void initialize(final Bootstrap<ServerConfiguration> bootstrap) {
    // Support reading config from resources dir and processing env variables for configs
    bootstrap.setConfigurationSourceProvider(new SubstitutingSourceProvider(
        new ResourceConfigurationSourceProvider(),
        new EnvironmentVariableSubstitutor(false)));
  }

  @Override
  protected void bootstrapLogging() {
    // Disables bootstrapping of logback via dropwizard (let us use logback.xml or other log framework)
  }
}
