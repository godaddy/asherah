use std::fmt;

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_default_partition() {
        let partition = DefaultPartition::new("id123", "service", "product");
        assert_eq!(partition.system_key_id(), "_SK_service_product");
        assert_eq!(partition.intermediate_key_id(), "_IK_id123_service_product");
        assert!(partition.is_valid_intermediate_key_id("_IK_id123_service_product"));
        assert!(!partition.is_valid_intermediate_key_id("_IK_wrong_service_product"));
    }
}

/// Trait for partition naming and validation
pub trait Partition: Send + Sync + fmt::Debug {
    /// Returns the system key ID for this partition
    fn system_key_id(&self) -> String;

    /// Returns the intermediate key ID for this partition
    fn intermediate_key_id(&self) -> String;

    /// Checks if the given ID is a valid intermediate key ID for this partition
    fn is_valid_intermediate_key_id(&self, id: &str) -> bool;
}

/// Default implementation of the Partition trait
#[derive(Debug, Clone)]
pub struct DefaultPartition {
    id: String,
    service: String,
    product: String,
}

impl DefaultPartition {
    /// Creates a new DefaultPartition
    pub fn new(
        partition_id: impl Into<String>,
        service: impl Into<String>,
        product: impl Into<String>,
    ) -> Self {
        Self {
            id: partition_id.into(),
            service: service.into(),
            product: product.into(),
        }
    }
}

impl Partition for DefaultPartition {
    fn system_key_id(&self) -> String {
        format!("_SK_{}_{}", self.service, self.product)
    }

    fn intermediate_key_id(&self) -> String {
        format!("_IK_{}_{}_{}", self.id, self.service, self.product)
    }

    fn is_valid_intermediate_key_id(&self, id: &str) -> bool {
        id == self.intermediate_key_id()
    }
}

/// Partition implementation with a suffix
#[derive(Debug, Clone)]
pub struct SuffixedPartition {
    inner: DefaultPartition,
    suffix: String,
}

impl SuffixedPartition {
    /// Creates a new SuffixedPartition
    pub fn new(
        partition_id: impl Into<String>,
        service: impl Into<String>,
        product: impl Into<String>,
        suffix: impl Into<String>,
    ) -> Self {
        Self {
            inner: DefaultPartition::new(partition_id, service, product),
            suffix: suffix.into(),
        }
    }
}

impl Partition for SuffixedPartition {
    fn system_key_id(&self) -> String {
        format!(
            "_SK_{}_{}_{}",
            self.inner.service, self.inner.product, self.suffix
        )
    }

    fn intermediate_key_id(&self) -> String {
        format!(
            "_IK_{}_{}_{}_{}",
            self.inner.id, self.inner.service, self.inner.product, self.suffix
        )
    }

    fn is_valid_intermediate_key_id(&self, id: &str) -> bool {
        // Match the Go implementation behavior: check for exact match or if it starts
        // with the default partition's intermediate key ID
        id == self.intermediate_key_id() || id.starts_with(&self.inner.intermediate_key_id())
    }
}
