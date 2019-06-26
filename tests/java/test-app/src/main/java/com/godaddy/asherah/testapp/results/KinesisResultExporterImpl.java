package com.godaddy.asherah.testapp.results;

import com.amazonaws.AmazonClientException;
import com.amazonaws.services.kinesis.AmazonKinesis;
import com.amazonaws.services.kinesis.AmazonKinesisClientBuilder;
import com.amazonaws.services.kinesis.model.PutRecordsRequest;
import com.amazonaws.services.kinesis.model.PutRecordsRequestEntry;
import com.amazonaws.services.kinesis.model.PutRecordsResult;
import com.godaddy.asherah.testapp.configuration.ServerConfiguration;

import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.nio.ByteBuffer;
import java.util.ArrayList;
import java.util.List;

public class KinesisResultExporterImpl implements ResultExporter {
  private static final Logger LOG = LoggerFactory.getLogger(KinesisResultExporterImpl.class);

  private final AmazonKinesis kinesisClient;
  private final String dataStream;
  private final List<PutRecordsRequestEntry> putRecordsRequestEntryList;

  public KinesisResultExporterImpl(final ServerConfiguration serverConfiguration) {
    this.dataStream = serverConfiguration.getKinesisDataStream();
    this.kinesisClient = AmazonKinesisClientBuilder.standard().build();
    this.putRecordsRequestEntryList = new ArrayList<>();
  }

  @Override
  public void appendTestResultToBatch(final JSONObject result) {
    PutRecordsRequestEntry putRecordsRequestEntry = new PutRecordsRequestEntry();
    putRecordsRequestEntry.setPartitionKey(Long.toString(System.currentTimeMillis()));
    putRecordsRequestEntry.setData(ByteBuffer.wrap(result.toString().getBytes()));
    putRecordsRequestEntryList.add(putRecordsRequestEntry);
  }

  @Override
  public void exportTestResultsBatch() {
    PutRecordsRequest putRecordsRequest = new PutRecordsRequest();
    putRecordsRequest.setStreamName(dataStream);
    putRecordsRequest.setRecords(putRecordsRequestEntryList);

    try {
      PutRecordsResult putRecordResult = kinesisClient.putRecords(putRecordsRequest);
      putRecordsRequestEntryList.clear();

      LOG.info("Put records with failed record count of {}", putRecordResult.getFailedRecordCount());
      LOG.debug("Put records result {}", putRecordResult);
    }
    catch (AmazonClientException ex) {
      LOG.error("Error sending records to Kinesis", ex);
    }
  }
}
