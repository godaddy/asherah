package com.godaddy.asherah.testapp.results;

import org.apache.http.HttpHost;
import org.elasticsearch.action.bulk.BulkItemResponse;
import org.elasticsearch.action.bulk.BulkRequest;
import org.elasticsearch.action.bulk.BulkResponse;
import org.elasticsearch.action.index.IndexRequest;
import org.elasticsearch.client.RestClient;
import org.elasticsearch.client.RestHighLevelClient;
import org.elasticsearch.common.xcontent.XContentType;
import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.godaddy.asherah.testapp.configuration.ServerConfiguration;
import com.godaddy.asherah.testapp.utils.DateTimeUtils;

import java.io.IOException;
import java.util.Arrays;
import java.util.List;
import java.util.stream.Collectors;

public class ElasticsearchResultExporterImpl implements ResultExporter {
  private static final Logger LOG = LoggerFactory.getLogger(ElasticsearchResultExporterImpl.class);
  private static final String TYPE = "results";

  private final String url;
  private final String indexPrefix;
  private final RestHighLevelClient restHighLevelClient;

  private BulkRequest bulkRequest;

  public ElasticsearchResultExporterImpl(final ServerConfiguration configuration) {
    this.url = configuration.getElasticsearchUrl();
    this.indexPrefix = configuration.getElasticsearchIndexPrefix();
    this.restHighLevelClient = new RestHighLevelClient(RestClient.builder(HttpHost.create(this.url)));
    this.bulkRequest = new BulkRequest();
  }

  public void appendTestResultToBatch(final JSONObject result) {
    String date = DateTimeUtils.getCurrentTimeAsUtcIsoDate();
    IndexRequest indexRequest = new IndexRequest(indexPrefix + "-" + date, TYPE);
    indexRequest.source(result.toString(), XContentType.JSON);

    bulkRequest.add(indexRequest);
  }

  @Override
  public void exportTestResultsBatch() {
    try {
      BulkResponse bulkResponse = restHighLevelClient.bulk(bulkRequest);

      List<String> failureMessages = Arrays.stream(bulkResponse.getItems())
          .filter(BulkItemResponse::isFailed)
          .map(BulkItemResponse::getFailureMessage)
          .collect(Collectors.toList());

      // Log overall result and errors, if any
      LOG.info("Pushed java test results to elasticsearch endpoint {} with failure count of {}", url, failureMessages.size());
      failureMessages.stream().forEach(LOG::error);

      // Always clear out request since we never retry and lost events don't matter here
      bulkRequest = new BulkRequest();
    }
    catch (IOException e) {
      LOG.error("Exception while exporting results to elastic endpoint '{}': ", url, e);
    }
  }

}
