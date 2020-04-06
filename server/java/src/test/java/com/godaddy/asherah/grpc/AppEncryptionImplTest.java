package com.godaddy.asherah.grpc;

import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import com.google.protobuf.ByteString;
import org.junit.jupiter.api.Test;

import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.time.temporal.ChronoUnit;

import static com.godaddy.asherah.grpc.Constants.*;
import static org.junit.jupiter.api.Assertions.*;

class AppEncryptionImplTest {

  AppEncryptionImpl appEncryption;
  long parentKeyMetaCreatedTime, ekrCreatedTime;
  String drrBytes, ekrBytes, parentKeyMetaKeyId;

  public AppEncryptionImplTest() {
    this.appEncryption = new AppEncryptionImpl();
    parentKeyMetaCreatedTime = Instant.now().getEpochSecond();
    ekrCreatedTime = Instant.now().minus(1, ChronoUnit.DAYS).getEpochSecond();
    parentKeyMetaKeyId = "someId";
    drrBytes = "someRandomBytes";
    ekrBytes = "ekrBytes";
  }

  @Test
  void testTransformJsonToDrr() {

    String actualJson =
      "{\"" + DRR_DATA + "\":\"" + drrBytes +
      "\",\"" + DRR_KEY + "\":{\"" + EKR_PARENTKEYMETA + "\":{\"" +
      "" + PARENTKEYMETA_KEYID + "\":\"" + parentKeyMetaKeyId + "\",\"" +
      "" + PARENTKEYMETA_CREATED + "\":" + parentKeyMetaCreatedTime + "}," +
      "\"" + EKR_KEY + "\":\"" + ekrBytes + "\"," +
      "\"" + EKR_CREATED + "\":" + ekrCreatedTime + "}}";

    JsonObject drrJson = new JsonParser().parse(actualJson).getAsJsonObject();
    AppEncryptionProtos.DataRowRecord dataRowRecord = appEncryption.transformJsonToDrr(drrJson);

    assertEquals(dataRowRecord.getData(), ByteString.copyFrom(drrBytes.getBytes(StandardCharsets.UTF_8)));
    assertEquals(dataRowRecord.getKey().getKey(), ByteString.copyFrom(ekrBytes.getBytes(StandardCharsets.UTF_8)));
    assertEquals(dataRowRecord.getKey().getCreated(), ekrCreatedTime);
    assertEquals(dataRowRecord.getKey().getParentKeyMeta().getKeyId(), parentKeyMetaKeyId);
    assertEquals(dataRowRecord.getKey().getParentKeyMeta().getCreated(), parentKeyMetaCreatedTime);
  }

  @Test
  void testTransformDrrToJson() {

    String expectedJson =
      "{\"" + DRR_DATA + "\":\"" + drrBytes +
        "\",\"" + DRR_KEY + "\":{\"" + EKR_PARENTKEYMETA + "\":{\"" +
        "" + PARENTKEYMETA_KEYID + "\":\"" + parentKeyMetaKeyId + "\",\"" +
        "" + PARENTKEYMETA_CREATED + "\":" + parentKeyMetaCreatedTime + "}," +
        "\"" + EKR_KEY + "\":\"" + ekrBytes + "\"," +
        "\"" + EKR_CREATED + "\":" + ekrCreatedTime + "}}";

    AppEncryptionProtos.DataRowRecord dataRowRecord = AppEncryptionProtos.DataRowRecord.newBuilder()
      .setData(ByteString.copyFrom(drrBytes.getBytes(StandardCharsets.UTF_8)))
      .setKey(AppEncryptionProtos.EnvelopeKeyRecord.newBuilder()
        .setParentKeyMeta(AppEncryptionProtos.KeyMeta.newBuilder()
          .setCreated(parentKeyMetaCreatedTime)
          .setKeyId(parentKeyMetaKeyId)
          .build())
        .setKey(ByteString.copyFrom(ekrBytes.getBytes(StandardCharsets.UTF_8)))
        .setCreated(ekrCreatedTime)
        .build())
      .build();

    JsonObject drrJson = appEncryption.transformDrrToJson(dataRowRecord);
    assertEquals(expectedJson, drrJson.toString());
  }
}
