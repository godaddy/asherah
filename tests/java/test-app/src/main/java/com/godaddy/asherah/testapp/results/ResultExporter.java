package com.godaddy.asherah.testapp.results;

import org.json.JSONObject;

public interface ResultExporter {

  void appendTestResultToBatch(JSONObject result);

  void exportTestResultsBatch();

}
