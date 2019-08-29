package com.godaddy.asherah.appencryption;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.*;

class PartitionTest {

    private final static String testPartitionId = "test_partition_id";
    private final static String testSystemId = "test_system_id";
    private final static String testProductId = "test_product_id";

    private Partition partition;

    @BeforeEach
    void setUp() {
        partition = new Partition(testPartitionId, testSystemId, testProductId);
    }

    @Test
    void testGetPartitionId() {
        assertEquals(testPartitionId, partition.getPartitionId());
    }

    @Test
    void testGetSystemId() {
        assertEquals(testSystemId, partition.getSystemId());
    }

    @Test
    void testGetProductId() {
        assertEquals(testProductId, partition.getProductId());
    }

    @Test
    void testGetSystemKeyId() {
        String systemKeyIdString = "_SK_" + testSystemId + "_" + testProductId;
        assertEquals(systemKeyIdString, partition.getSystemKeyId());
    }

    @Test
    void testGetIntermediateKeyId() {
        String intermediateKeyIdString = "_IK_" + testPartitionId + "_" + testSystemId + "_" + testProductId;
        assertEquals(intermediateKeyIdString, partition.getIntermediateKeyId());
    }

    @Test
    void testToString() {
        String toStringString = partition.getClass().getSimpleName() + "[partitionId=" + testPartitionId +
                ", systemId=" + testSystemId + ", productId=" + testProductId + "]";
        assertEquals(toStringString, partition.toString());
    }
}
