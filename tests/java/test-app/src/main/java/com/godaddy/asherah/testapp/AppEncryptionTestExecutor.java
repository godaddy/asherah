package com.godaddy.asherah.testapp;

import org.json.JSONObject;
import org.junit.platform.engine.DiscoverySelector;
import org.junit.platform.launcher.Launcher;
import org.junit.platform.launcher.core.LauncherDiscoveryRequestBuilder;
import org.junit.platform.launcher.core.LauncherFactory;
import org.junit.platform.launcher.listeners.SummaryGeneratingListener;
import org.junit.platform.launcher.listeners.TestExecutionSummary;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.godaddy.asherah.testapp.configuration.ServerConfiguration;
import com.godaddy.asherah.testapp.results.ElasticsearchResultExporterImpl;
import com.godaddy.asherah.testapp.results.KinesisResultExporterImpl;
import com.godaddy.asherah.testapp.results.LogResultExporterImpl;
import com.godaddy.asherah.testapp.results.ResultExporter;

import java.time.Instant;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;

import static com.godaddy.asherah.testapp.testhelpers.Constants.*;
import static org.junit.platform.engine.discovery.DiscoverySelectors.selectPackage;

public class AppEncryptionTestExecutor {
  private static final Logger LOG = LoggerFactory.getLogger(AppEncryptionTestExecutor.class);

  private final List<ResultExporter> resultExporters = new ArrayList<>();
  private final Map<String, String> testSetupMap = new HashMap<>();

  public AppEncryptionTestExecutor(final ServerConfiguration configuration) {
    if (configuration.shouldExportResultsToKinesis()) {
      this.resultExporters.add(new KinesisResultExporterImpl(configuration));
    }

    if (configuration.shouldExportResultsToElasticsearch()) {
      this.resultExporters.add(new ElasticsearchResultExporterImpl(configuration));
    }

    if (configuration.shouldExportResultsToLog()) {
      this.resultExporters.add(new LogResultExporterImpl());
    }

    // Save off any interesting test setup that we'd want in generated events/results
    testSetupMap.put(TEST_SUMMARY_METASTORE_TYPE, configuration.getMetaStoreType());
    testSetupMap.put(TEST_SUMMARY_KEY_MANAGEMENT_TYPE, configuration.getKmsType());
  }

  public String runPackage(final String endpoint, final String packageName, final Map<String, ?> parameterMap) {
    return runSuite(endpoint, Arrays.asList(selectPackage(packageName)), parameterMap);
  }

  String runSuite(final String endpoint, final List<DiscoverySelector> discoverySelectors,
      final Map<String, ?> parameterMap) {
    JSONObject resultJson = new JSONObject();
    resultJson.put(TEST_ENDPOINT, endpoint);
    resultJson.put(TEST_EXECUTION_TIMESTAMP, Instant.now().toString());

    LauncherDiscoveryRequestBuilder request = LauncherDiscoveryRequestBuilder.request()
        .selectors(discoverySelectors);
    if (parameterMap != null) {
      // If params provided, convert values to strings and place in ConfigurationParameters to extract from ExtensionContext
      request.configurationParameters(
          parameterMap.entrySet().stream()
            .collect(Collectors.toMap(
                Map.Entry::getKey,
                e -> e.getValue().toString()
            ))
      );
    }

    final SummaryGeneratingListener summaryGeneratingListener = new SummaryGeneratingListener();
    final AppEncryptionTestListener appEncryptionTestListener = new AppEncryptionTestListener(this.resultExporters,
        testSetupMap);

    Launcher launcher = LauncherFactory.create();
    launcher.registerTestExecutionListeners(summaryGeneratingListener, appEncryptionTestListener);
    launcher.execute(request.build());

    TestExecutionSummary summary = summaryGeneratingListener.getSummary();
    JSONObject finalResultJson = generateSummaryResponse(summary, resultJson);
    return finalResultJson.toString();
  }

  private JSONObject generateSummaryResponse(final TestExecutionSummary summary, final JSONObject resultJson) {
    resultJson.put(TEST_SUMMARY_TOTAL_TESTS_COUNT, summary.getTestsFoundCount());
    resultJson.put(TEST_SUMMARY_SUCCESSFUL_TESTS_COUNT, summary.getTestsSucceededCount());
    resultJson.put(TEST_SUMMARY_FAILED_TESTS_COUNT, summary.getTestsFailedCount());

    List<String> failureList = new ArrayList<>();
    for (TestExecutionSummary.Failure failure : summary.getFailures()) {
      LOG.error("{} exception dump", failure.getTestIdentifier().toString(), failure.getException());
      failureList.add(failure.getTestIdentifier().getDisplayName() + ":" + failure.getException().toString());
    }

    resultJson.put(TEST_SUMMARY_FAILURE_LIST, failureList);
    LOG.info("results response: {}", resultJson);

    return resultJson;
  }

}
