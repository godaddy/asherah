Feature: Encrypt Data using RDBMS metastore and static KMS
  I want to encrypt data

  Scenario Outline: Encrypting Data
    Given I have "<data>"
    When I encrypt the data
    Then I should get encrypted_data
    And encrypted_data should not be equal to data
    Examples:
      | data                 |
      | mySuperSecretPayload |
