package com.godaddy.asherah.appencryption;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;

class DefaultPartitionTest {

    private final static String testPartitionId = "test_partition_id";
    private final static String testServiceId = "test_service_id";
    private final static String testProductId = "test_product_id";

    private Partition partition;

    @BeforeEach
    void setUp() {
        partition = new DefaultPartition(testPartitionId, testServiceId, testProductId);
    }

    @Test
    void testGetSystemKeyId() {
        String systemKeyIdString = "_SK_" + testServiceId + "_" + testProductId;
        assertEquals(systemKeyIdString, partition.getSystemKeyId());
    }

    @Test
    void testGetIntermediateKeyId() {
        String intermediateKeyIdString = "_IK_" + testPartitionId + "_" + testServiceId + "_" + testProductId;
        assertEquals(intermediateKeyIdString, partition.getIntermediateKeyId());
    }

    @Test
    void testToString() {
        String toStringString = partition.getClass().getSimpleName() + "[partitionId=" + testPartitionId +
                ", serviceId=" + testServiceId + ", productId=" + testProductId + "]";
        assertEquals(toStringString, partition.toString());
    }
}
