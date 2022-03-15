Given 'I have encrypted_data from {string}' do |file_name|
  @encrypted_data = File.read(File.join(TMP_DIR, file_name))
end

When 'I decrypt the encrypted_data' do
  @decrypted_data = Asherah.decrypt_from_json('partition', @encrypted_data)
end

Then 'I should get decrypted_data' do
  expect(@decrypted_data).not_to eq(nil)
end

Then 'decrypted_data should be equal to {string}' do |data|
  expect(@decrypted_data).to eq(data)
end
