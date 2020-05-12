Feature: Decrypt Data using RDBMS metastore and static KMS
  I want to decrypt data

  Scenario Outline: Decrypting Data
    Given I have encrypted_data from "<file_name>"
    When I decrypt the encrypted_data
    Then I should get decrypted_data
    And decrypted_data should be equal to "<data>"
    Examples:
      | data                 | file_name              |
      | mySuperSecretPayload | java_encrypted         |
      | mySuperSecretPayload | csharp_encrypted       |
      | mySuperSecretPayload | go_encrypted           |
      | mySuperSecretPayload | sidecar_go_encrypted   |
      | mySuperSecretPayload | sidecar_java_encrypted |
