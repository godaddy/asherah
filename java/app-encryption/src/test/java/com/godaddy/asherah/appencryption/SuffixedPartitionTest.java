package com.godaddy.asherah.appencryption;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;

class SuffixedPartitionTest {

    private final static String testPartitionId = "test_partition_id";
    private final static String testServiceId = "test_service_id";
    private final static String testProductId = "test_product_id";
    private final static String testRegionSuffix = "test_region_suffix";


    private Partition partition;

    @BeforeEach
    void setUp() {
        partition = new SuffixedPartition(testPartitionId, testServiceId, testProductId, testRegionSuffix);
    }

    @Test
    void testGetSystemKeyId() {
        String systemKeyIdString = "_SK_" + testServiceId + "_" + testProductId + "_" + testRegionSuffix;
        assertEquals(systemKeyIdString, partition.getSystemKeyId());
    }

    @Test
    void testGetIntermediateKeyId() {
        String intermediateKeyIdString =
          "_IK_" + testPartitionId + "_" + testServiceId + "_" + testProductId + "_" + testRegionSuffix;
        assertEquals(intermediateKeyIdString, partition.getIntermediateKeyId());
    }

    @Test
    void testToString() {
        String toStringString = partition.getClass().getSimpleName() + "[partitionId=" + testPartitionId +
          ", serviceId=" + testServiceId + ", productId=" + testProductId + ", regionSuffix=" + testRegionSuffix + "]";
        assertEquals(toStringString, partition.toString());
    }
}
