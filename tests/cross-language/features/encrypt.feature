Feature: Encrypt Data using RDBMS metastore and static KMS
  I want to encrypt data

  Scenario Outline: Encrypting Data
    Given I have "<data>"
    When I encrypt the data
    Then I get should get encrypted_data
    And encrypted_data should not equal data
    Examples:
      | data                 |
      | mySuperSecretPayload |
