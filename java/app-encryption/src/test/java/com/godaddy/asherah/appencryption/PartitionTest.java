package com.godaddy.asherah.appencryption;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.*;

class PartitionTest {

    private final static String testPartitionId = "test_partition_id";
    private final static String testServiceId = "test_service_id";
    private final static String testProductId = "test_product_id";

    private Partition partition;

    @BeforeEach
    void setUp() {
        partition = new DefaultPartition(testPartitionId, testServiceId, testProductId);
    }

    @Test
    void testGetPartitionId() {
        assertEquals(testPartitionId, partition.getPartitionId());
    }

    @Test
    void testGetServiceId() {
        assertEquals(testServiceId, partition.getServiceId());
    }

    @Test
    void testGetProductId() {
        assertEquals(testProductId, partition.getProductId());
    }
}
