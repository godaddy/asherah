package com.godaddy.asherah.appencryption.persistence;

import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.sql.SQLException;
import java.sql.SQLIntegrityConstraintViolationException;
import java.sql.Timestamp;
import java.time.Instant;
import java.util.Optional;

import io.micrometer.core.instrument.Metrics;
import io.micrometer.core.instrument.Timer;

import org.json.JSONException;
import org.json.JSONObject;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.godaddy.asherah.appencryption.exceptions.AppEncryptionException;
import com.godaddy.asherah.appencryption.utils.MetricsUtil;

import javax.sql.DataSource;

/**
 * Provides a JDBC based implementation of {@link Metastore} to store and retrieve
 * {@link com.godaddy.asherah.appencryption.utils.Json} values for system and intermediate keys.It uses the table name
 * "encryption_key". Creation time is stored in UTC.
 */
public class JdbcMetastoreImpl implements Metastore<JSONObject> {
  private static final Logger logger = LoggerFactory.getLogger(JdbcMetastoreImpl.class);

  static final String LOAD_QUERY        = "SELECT key_record from encryption_key where id = ? and created = ?";
  static final String STORE_QUERY       = "INSERT INTO encryption_key (id, created, key_record) VALUES (?, ?, ?)";
  static final String LOAD_LATEST_QUERY = "SELECT key_record from encryption_key " +
      "where id = ? order by created DESC limit 1";

  static final String KEY_RECORD = "key_record";

  private final Timer loadTimer = Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".metastore.jdbc.load");
  private final Timer loadLatestTimer = Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".metastore.jdbc.loadlatest");
  private final Timer storeTimer = Metrics.timer(MetricsUtil.AEL_METRICS_PREFIX + ".metastore.jdbc.store");

  private final DataSource dataSource;

  /**
   * Initialize a {@code JdbcMetastoreImpl} builder using the provided parameter.
   *
   * @param dataSource The {@link javax.sql.DataSource} object used to connect to the database.
   * @return The current {@link Builder} step.
   */
  public static Builder newBuilder(final DataSource dataSource) {
    return new Builder(dataSource);
  }

  JdbcMetastoreImpl(final DataSource dataSource) {
    this.dataSource = dataSource;
  }

  Connection getConnection() throws SQLException {
    return dataSource.getConnection();
  }

  Optional<JSONObject> executeQueryAndLoadJsonObjectFromKey(final PreparedStatement preparedStatement)
      throws SQLException {

    try (ResultSet resultSet = preparedStatement.executeQuery()) {

      if (resultSet.next()) {
        String keyString = resultSet.getString(KEY_RECORD);

        try {
          JSONObject jsonObject = new JSONObject(keyString);
          return Optional.of(jsonObject);
        }
        catch (JSONException e) {
          logger.error("Failed to create JSON from key", e);
        }
      }
    }

    return Optional.empty();
  }

  /**
   * Uses the {@link JdbcMetastoreImpl#LOAD_QUERY} to retrieve the value associated with the keyId and time it was
   * created.
   *
   * @param keyId The keyId part of the lookup key.
   * @param created The created time part of the lookup key.
   * @return The value associated with the keyId and created tuple, if any.
   */
  @Override
  public Optional<JSONObject> load(final String keyId, final Instant created) {
    return loadTimer.record(() -> {
      try (Connection connection = getConnection();
           PreparedStatement preparedStatement = connection.prepareStatement(LOAD_QUERY)) {

        preparedStatement.setString(1, keyId);
        preparedStatement.setTimestamp(2, Timestamp.from(created));

        return executeQueryAndLoadJsonObjectFromKey(preparedStatement);
      }
      catch (SQLException se) {
        logger.error("Metastore error", se);
        return Optional.empty();
      }
    });
  }

  /**
   * Uses the {@link JdbcMetastoreImpl#LOAD_LATEST_QUERY} to retrieve the latest value associated with the keyId.
   *
   * @param keyId The keyId part of the lookup key.
   * @return The latest value associated with the keyId, if any.
   */
  @Override
  public Optional<JSONObject> loadLatest(final String keyId) {
    return loadLatestTimer.record(() -> {
      try (Connection connection = getConnection();
           PreparedStatement preparedStatement = connection.prepareStatement(LOAD_LATEST_QUERY)) {

        preparedStatement.setString(1, keyId);

        return executeQueryAndLoadJsonObjectFromKey(preparedStatement);
      }
      catch (SQLException se) {
        logger.error("Metastore error", se);
        return Optional.empty();
      }
    });
  }

  /**
   * Uses the {@link JdbcMetastoreImpl#STORE_QUERY} to store the value using the specified keyId and created time.
   *
   * @param keyId The keyId part of the lookup key.
   * @param created The created time part of the lookup key.
   * @param value The value to store.
   * @return {@code true} if the store succeeded, false if the store failed for a known condition
   *         e.g., trying to save a duplicate value should return false, not throw an exception.
   */
  @Override
  public boolean store(final String keyId, final Instant created, final JSONObject value) {
    return storeTimer.record(() -> {
      try (Connection connection = getConnection();
           PreparedStatement preparedStatement = connection.prepareStatement(STORE_QUERY)) {
        int i = 1;
        preparedStatement.setString(i++, keyId);
        preparedStatement.setTimestamp(i++, Timestamp.from(created));
        preparedStatement.setString(i++, value.toString());

        return preparedStatement.executeUpdate() == 1;
      }
      catch (SQLIntegrityConstraintViolationException iv) {
        // Duplicate key exists
        logger.info("Attempted to create duplicate key: {} {}", keyId, created);
        return false;
      }
      catch (SQLException se) {
        logger.error("Metastore error during store", se);
        throw new AppEncryptionException("Metastore error", se);
      }
    });
  }

  public static final class Builder {

    private final DataSource dataSource;

    private Builder(final DataSource dataSource) {
      this.dataSource = dataSource;
    }

    /**
     * Builds the {@code JdbcMetastoreImpl} object.
     *
     * @return The fully instantiated {@code JdbcMetastoreImpl} object.
     */
    public JdbcMetastoreImpl build() {
      return new JdbcMetastoreImpl(dataSource);
    }
  }
}
