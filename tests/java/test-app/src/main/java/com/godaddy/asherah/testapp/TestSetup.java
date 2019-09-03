package com.godaddy.asherah.testapp;

import com.codahale.metrics.ConsoleReporter;
import com.codahale.metrics.MetricRegistry;
import com.godaddy.asherah.appencryption.kms.KeyManagementService;
import com.godaddy.asherah.appencryption.persistence.Metastore;
import com.godaddy.asherah.testapp.configuration.ServerConfiguration;
import com.godaddy.asherah.testapp.utils.KeyManagementServiceFactory;
import com.godaddy.asherah.testapp.utils.MetastoreFactory;

import io.micrometer.core.instrument.Clock;
import io.micrometer.core.instrument.MeterRegistry;
import io.micrometer.core.instrument.Metrics;
import io.micrometer.core.instrument.dropwizard.DropwizardConfig;
import io.micrometer.core.instrument.dropwizard.DropwizardMeterRegistry;
import io.micrometer.core.instrument.util.HierarchicalNameMapper;

import java.util.concurrent.TimeUnit;

import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;


public final class TestSetup {
  private static final Logger LOG = LoggerFactory.getLogger(TestSetup.class);

  // volatile for hacky thread safety on read in non-synchronized methods
  private static volatile boolean initialized = false;

  private static KeyManagementService keyManagementService;
  private static Metastore<JSONObject> metastore;

  private TestSetup() {
  }

  // One-time initialization code. Very clunky due to the fact we're using junit for running the test calls.
  // Synchronized the entire method for simple thread safety on write path.
  static synchronized void init(final ServerConfiguration configuration) {
    if (!initialized) {

      String metaStore = configuration.getMetaStoreType();
      metastore = MetastoreFactory.createMetastore(configuration, metaStore);

      String kms = configuration.getKmsType();
      keyManagementService = KeyManagementServiceFactory.createKeyManagementService(configuration, kms);

      // Setup console metrics output (need to use dropwizard hack as micrometer doesn't have console out of the box)
      // Consider enabling CloudWatch metrics at some point.
      MetricRegistry consoleRegistry = new MetricRegistry();
      ConsoleReporter consoleReporter = ConsoleReporter.forRegistry(consoleRegistry)
          .convertRatesTo(TimeUnit.SECONDS)
          .convertDurationsTo(TimeUnit.MILLISECONDS)
          .build();
      Metrics.addRegistry(createConsoleMeterRegistry(consoleRegistry));

      // Report every 1m and add shutdown hook for final report on exit
      consoleReporter.start(1, TimeUnit.MINUTES);
      Runtime.getRuntime().addShutdownHook(new Thread(() -> consoleReporter.report()));

      // write volatile last to force other members to be flushed to memory
      initialized = true;

      LOG.info("one-time initialization complete!");
    }
  }

  public static KeyManagementService getKeyManagementService() {
    // Read volatile first to force other members to be read from memory
    if (!initialized) {
      throw new IllegalStateException("initialization has not been run yet!");
    }

    return keyManagementService;
  }

  public static Metastore<JSONObject> getMetastore() {
    // Read volatile first to force other members to be read from memory
    if (!initialized) {
      throw new IllegalStateException("initialization has not been run yet!");
    }

    return metastore;
  }

  public static boolean isInitialized() {
    return initialized;
  }

  private static MeterRegistry createConsoleMeterRegistry(final MetricRegistry consoleRegistry) {
    DropwizardConfig consoleConfig = new DropwizardConfig() {

      @Override
      public String prefix() {
        return "console";
      }

      @Override
      public String get(final String key) {
        return null;
      }
    };

    return new DropwizardMeterRegistry(consoleConfig, consoleRegistry, HierarchicalNameMapper.DEFAULT, Clock.SYSTEM) {
      @Override
      protected Double nullGaugeValue() {
        return null;
      }
    };
  }

}
