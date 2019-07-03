package com.godaddy.asherah.testapp.results;

import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

public class LogResultExporterImpl implements ResultExporter {
  private static final Logger LOG = LoggerFactory.getLogger(LogResultExporterImpl.class);

  @Override
  public void appendTestResultToBatch(final JSONObject result) {
    LOG.info(result.toString());
  }

  @Override
  public void exportTestResultsBatch() {
  }
}
