package com.godaddy.asherah.appencryption;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.*;

class AppEncryptionPartitionTest {

    private final static String testPartitionId = "test_partition_id";
    private final static String testSystemId = "test_system_id";
    private final static String testProductId = "test_product_id";

    private AppEncryptionPartition appEncryptionPartition;

    @BeforeEach
    void setUp() {
        appEncryptionPartition = new AppEncryptionPartition(testPartitionId, testSystemId, testProductId);
    }

    @Test
    void testGetPartitionId() {
        assertEquals(testPartitionId, appEncryptionPartition.getPartitionId());
    }

    @Test
    void testGetSystemId() {
        assertEquals(testSystemId, appEncryptionPartition.getSystemId());
    }

    @Test
    void testGetProductId() {
        assertEquals(testProductId, appEncryptionPartition.getProductId());
    }

    @Test
    void testGetSystemKeyId() {
        String systemKeyIdString = "_SK_" + testSystemId + "_" + testProductId;
        assertEquals(systemKeyIdString, appEncryptionPartition.getSystemKeyId());
    }

    @Test
    void testGetIntermediateKeyId() {
        String intermediateKeyIdString = "_IK_" + testPartitionId + "_" + testSystemId + "_" + testProductId;
        assertEquals(intermediateKeyIdString, appEncryptionPartition.getIntermediateKeyId());
    }

    @Test
    void testToString() {
        String toStringString = appEncryptionPartition.getClass().getSimpleName() + "[partitionId=" + testPartitionId +
                ", systemId=" + testSystemId + ", productId=" + testProductId + "]";
        assertEquals(toStringString, appEncryptionPartition.toString());
    }
}
