#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_default_partition() {
        let partition = DefaultPartition::new("partition_id", "service_name", "product_name");

        // Test system key ID format
        assert_eq!(partition.system_key_id(), "_SK_service_name_product_name");

        // Test intermediate key ID format
        assert_eq!(
            partition.intermediate_key_id(),
            "_IK_partition_id_service_name_product_name"
        );

        // Test intermediate key ID validation
        assert!(partition.is_valid_intermediate_key_id(
            "_IK_partition_id_service_name_product_name"
        ));

        // Test intermediate key ID rejection
        assert!(!partition.is_valid_intermediate_key_id(
            "_IK_different_partition_service_name_product_name"
        ));
    }

    #[test]
    fn test_suffixed_partition() {
        let partition = SuffixedPartition::new(
            "partition_id",
            "service_name",
            "product_name",
            "suffix"
        );

        // Test system key ID format
        assert_eq!(partition.system_key_id(), "_SK_service_name_product_name_suffix");

        // Test intermediate key ID format
        assert_eq!(
            partition.intermediate_key_id(),
            "_IK_partition_id_service_name_product_name_suffix"
        );

        // Test exact match validation
        assert!(partition.is_valid_intermediate_key_id(
            "_IK_partition_id_service_name_product_name_suffix"
        ));

        // Test prefix match validation (default partition's ID is a prefix)
        assert!(partition.is_valid_intermediate_key_id(
            "_IK_partition_id_service_name_product_name_different_suffix"
        ));

        // Test rejection of non-matching ID
        assert!(!partition.is_valid_intermediate_key_id(
            "_IK_different_partition_service_name_product_name_suffix"
        ));
    }
}