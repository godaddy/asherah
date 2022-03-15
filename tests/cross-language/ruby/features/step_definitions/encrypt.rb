Given 'I have {string}' do |data|
  @data = data
end

When 'I encrypt the data' do
  @encrypted_data = Base64.strict_encode64(Asherah.encrypt_to_json('partition', @data))
  Asherah.shutdown
end

Then 'I should get encrypted_data' do
  File.write(File.join(TMP_DIR, FILE_NAME), @encrypted_data)
end

Then 'encrypted_data should not be equal to data' do
  expect(@encrypted_data).not_to eq(@data)
end
