package com.godaddy.asherah.testapp;

import org.json.JSONObject;
import org.junit.platform.engine.TestExecutionResult;
import org.junit.platform.launcher.TestExecutionListener;
import org.junit.platform.launcher.TestIdentifier;
import org.junit.platform.launcher.TestPlan;

import com.godaddy.asherah.testapp.results.ResultExporter;
import com.godaddy.asherah.testapp.utils.DateTimeUtils;

import java.time.Instant;
import java.util.List;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

public class AppEncryptionTestListener implements TestExecutionListener {
  private final List<ResultExporter> resultExporters;
  private final long runIdentifier;
  private final Map<String, String> testSetupMap;
  private ConcurrentHashMap<String, Instant> timingValues = new ConcurrentHashMap<>();

  public AppEncryptionTestListener(final List<ResultExporter> resultExporters,
                                   final Map<String, String> testSetupMap,
                                   final long runIdentifier) {
    this.resultExporters = resultExporters;
    this.testSetupMap = testSetupMap;
    this.runIdentifier = runIdentifier;
  }

  public AppEncryptionTestListener(final List<ResultExporter> resultExporters,
                                   final Map<String, String> testConfiguration) {
    this(resultExporters, testConfiguration, Instant.now().toEpochMilli());
  }

  @Override
  public void executionStarted(final TestIdentifier testIdentifier) {
    timingValues.put(testIdentifier.getUniqueId(), Instant.now());
  }

  @Override
  public void executionFinished(final TestIdentifier testIdentifier, final TestExecutionResult testExecutionResult) {
    Instant stop = Instant.now();
    Instant start = (Instant) timingValues.get(testIdentifier.getUniqueId());
    long duration = stop.toEpochMilli() - start.toEpochMilli();
    JSONObject jsonObject = new JSONObject();

    // Trying a few things from this article:
    // https://www.swtestacademy.com/reporting-test-results-tesults-junit5-jupiter/
    if (testIdentifier.getParentId().isPresent()) {
      String separator = "class:";
      String suite = testIdentifier.getParentId().get();
      suite = suite.substring(suite.indexOf(separator) + separator.length(), suite.lastIndexOf("]"));
      jsonObject.put("suite", suite);
    }

    String name = testIdentifier.getDisplayName();
    if (name.contains("(")) {
      jsonObject.put("display_name", name.substring(0, name.lastIndexOf("(")));
    }
    else {
      jsonObject.put("display_name", name);
    }

    // Copy any interesting test config meta into event
    this.testSetupMap.forEach(jsonObject::put);

    jsonObject.put("description", testIdentifier.getDisplayName());
    jsonObject.put("timestamp", DateTimeUtils.getInstantAsUtcIsoOffsetDateTime(start));
    jsonObject.put("run_id", runIdentifier);
    jsonObject.put("test_id", testIdentifier.getUniqueId());
    jsonObject.put("legacy_name", testIdentifier.getLegacyReportingName());
    jsonObject.put("duration", duration);
    jsonObject.put("passed", testExecutionResult.getStatus() == TestExecutionResult.Status.SUCCESSFUL);

    testExecutionResult.getThrowable().ifPresent(throwable -> jsonObject.put("throwable", throwable.getMessage()));

    for (ResultExporter exporter : this.resultExporters) {
      exporter.appendTestResultToBatch(jsonObject);
    }
  }

  @Override
  public void testPlanExecutionFinished(final TestPlan testPlan) {
    for (ResultExporter exporter : this.resultExporters) {
      exporter.exportTestResultsBatch();
    }
  }
}
