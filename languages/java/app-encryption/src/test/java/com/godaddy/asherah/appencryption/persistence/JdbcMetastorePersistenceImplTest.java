package com.godaddy.asherah.appencryption.persistence;

import org.json.JSONObject;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.Spy;
import org.mockito.junit.jupiter.MockitoExtension;

import com.godaddy.asherah.appencryption.exceptions.AppEncryptionException;

import javax.sql.DataSource;
import java.sql.*;
import java.time.Instant;
import java.util.Optional;

import static com.godaddy.asherah.appencryption.persistence.JdbcMetastorePersistenceImpl.*;
import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
class JdbcMetastorePersistenceImplTest {

  private static final String KEY_STRING =
      "{\"ParentKeyMeta\"" +
          ":{\"KeyId\":\"_SK_api_ecomm\"," +
          "\"Created\":1541461380}," +
          "\"Key\":\"mWT/x4RvIFVFE2BEYV1IB9FMM8sWN1sK6YN5bS2UyGR+9RSZVTvp/bcQ6PycW6kxYEqrpA+aV4u04jOr\"," +
          "\"Created\":1541461380}";

  private static final String KEY_STRING_NO_PARENTKEYMETA =
      "{\"Key\":\"mWT/x4RvIFVFE2BEYV1IB9FMM8sWN1sK6YN5bS2UyGR+9RSZVTvp/bcQ6PycW6kxYEqrpA+aV4u04jOr\"," +
      "\"Created\":1541461380}";

  private static final String MALFORMED_KEY_STRING = "{\"ParentKeyMeta\":{\"KeyId\"";
  private static final String KEY = "some_key";

  @InjectMocks
  @Spy
  JdbcMetastorePersistenceImpl jdbcMetastorePersistenceImpl;

  @Mock
  DataSource dataSourceMock;

  @Mock
  Connection connectionMock;

  @Mock
  PreparedStatement preparedStatementMock;

  @Mock
  ResultSet resultSetMock;

  @ParameterizedTest
  @ValueSource(strings = {KEY_STRING, KEY_STRING_NO_PARENTKEYMETA})
  void testExecuteQueryAndLoadJsonObjectFromKey(String keyString) throws SQLException {

    when(preparedStatementMock.executeQuery()).thenReturn(resultSetMock);

    when(resultSetMock.next()).thenReturn(true);
    when(resultSetMock.getString(KEY_RECORD)).thenReturn(keyString);

    Optional<JSONObject> actualJsonObject =
        jdbcMetastorePersistenceImpl.executeQueryAndLoadJsonObjectFromKey(preparedStatementMock);

    assertNotNull(actualJsonObject.get());
    assertEquals(keyString, actualJsonObject.get().toString());
  }

  @Test
  void testExecuteQueryAndLoadJsonObjectFromKeyWithNoResultShouldReturnEmpty() throws SQLException {
    when(preparedStatementMock.executeQuery()).thenReturn(resultSetMock);

    when(resultSetMock.next()).thenReturn(false);

    Optional<JSONObject> actualJsonObject =
        jdbcMetastorePersistenceImpl.executeQueryAndLoadJsonObjectFromKey(preparedStatementMock);

    assertEquals(Optional.empty(), actualJsonObject);
  }

  @Test
  void testExecuteQueryAndLoadJsonObjectFromKeyWithException() throws SQLException {

    when(preparedStatementMock.executeQuery()).thenReturn(resultSetMock);

    when(resultSetMock.next()).thenReturn(true);
    when(resultSetMock.getString(KEY_RECORD)).thenReturn(MALFORMED_KEY_STRING);

    Optional<JSONObject> actualValue =
        jdbcMetastorePersistenceImpl.executeQueryAndLoadJsonObjectFromKey(preparedStatementMock);

    assertEquals(Optional.empty(), actualValue);
  }

  @Test
  void testLoad() throws SQLException {

    when(jdbcMetastorePersistenceImpl.getConnection()).thenReturn(connectionMock);

    when(connectionMock.prepareStatement(LOAD_QUERY)).thenReturn(preparedStatementMock);

    doNothing().when(preparedStatementMock).setString(any(Integer.class), anyString());
    doNothing().when(preparedStatementMock).setTimestamp(any(Integer.class), any(Timestamp.class));
    doReturn(Optional.of(new JSONObject(KEY_STRING)))
        .when(jdbcMetastorePersistenceImpl)
        .executeQueryAndLoadJsonObjectFromKey(preparedStatementMock);

    Optional<JSONObject> actualJsonObject = jdbcMetastorePersistenceImpl.load(KEY, Instant.now());

    assertNotNull(actualJsonObject.get());
    assertEquals(KEY_STRING, actualJsonObject.get().toString());
  }

  @Test
  void testLoadWithSQLException() throws SQLException {

    when(jdbcMetastorePersistenceImpl.getConnection()).thenThrow(SQLException.class);

    Optional<JSONObject> actualJsonObject = jdbcMetastorePersistenceImpl.load(KEY, Instant.now());

    assertEquals(Optional.empty(), actualJsonObject);
  }


  @Test
  void testLoadLatestValue() throws SQLException {

    when(jdbcMetastorePersistenceImpl.getConnection()).thenReturn(connectionMock);

    when(connectionMock.prepareStatement(LOAD_LATEST_QUERY)).thenReturn(preparedStatementMock);
    doNothing().when(preparedStatementMock).setString(any(Integer.class), anyString());
    doReturn(Optional.of(new JSONObject(KEY_STRING)))
        .when(jdbcMetastorePersistenceImpl)
        .executeQueryAndLoadJsonObjectFromKey(preparedStatementMock);

    Optional<JSONObject> actualJsonObject = jdbcMetastorePersistenceImpl.loadLatestValue(KEY);

    assertNotNull(actualJsonObject.get());
    assertEquals(KEY_STRING, actualJsonObject.get().toString());
  }

  @Test
  void testLoadLatestValueWithSQLException() throws SQLException {

    when(jdbcMetastorePersistenceImpl.getConnection()).thenThrow(SQLException.class);

    Optional<JSONObject> actualJsonObject = jdbcMetastorePersistenceImpl.loadLatestValue(KEY);

    assertEquals(Optional.empty(), actualJsonObject);
  }

  @Test
  void testStore() throws SQLException {

    when(jdbcMetastorePersistenceImpl.getConnection()).thenReturn(connectionMock);

    when(connectionMock.prepareStatement(STORE_QUERY)).thenReturn(preparedStatementMock);

    Instant now = Instant.now();
    JSONObject jsonObject = new JSONObject();

    when(preparedStatementMock.executeUpdate()).thenReturn(1);

    boolean actualValue = jdbcMetastorePersistenceImpl.store(KEY, now, jsonObject);

    assertTrue(actualValue);
    // verify PS ordering isn't broken
    int i = 1;
    verify(preparedStatementMock).setString(i++, KEY);
    verify(preparedStatementMock).setTimestamp(i++, Timestamp.from(now));
    verify(preparedStatementMock).setString(i, jsonObject.toString());
  }

  @Test
  void testStoreWithMultipleUpdatedShouldReturnFalse() throws SQLException {
    when(jdbcMetastorePersistenceImpl.getConnection()).thenReturn(connectionMock);

    when(connectionMock.prepareStatement(STORE_QUERY)).thenReturn(preparedStatementMock);

    when(preparedStatementMock.executeUpdate()).thenReturn(2);

    JSONObject jsonObject = new JSONObject();
    Instant now = Instant.now();
    boolean actualValue = jdbcMetastorePersistenceImpl.store(KEY, now, jsonObject);

    assertFalse(actualValue);
    // verify PS ordering isn't broken
    int i = 1;
    verify(preparedStatementMock).setString(i++, KEY);
    verify(preparedStatementMock).setTimestamp(i++, Timestamp.from(now));
    verify(preparedStatementMock).setString(i, jsonObject.toString());
  }

  @Test
  void testStoreWithSQLException() throws SQLException {

    when(jdbcMetastorePersistenceImpl.getConnection()).thenThrow(SQLException.class);

    assertThrows(AppEncryptionException.class,
        () -> jdbcMetastorePersistenceImpl.store(KEY, Instant.now(), new JSONObject()));
  }

  @Test
  void testStoreWithSQLIntegrityConstraintViolationException() throws SQLException {

    when(jdbcMetastorePersistenceImpl.getConnection()).thenReturn(connectionMock);
    when(connectionMock.prepareStatement(STORE_QUERY)).thenReturn(preparedStatementMock);
    when(preparedStatementMock.executeUpdate()).thenThrow(SQLIntegrityConstraintViolationException.class);

    boolean actualValue = jdbcMetastorePersistenceImpl.store(KEY, Instant.now(), new JSONObject());

    assertFalse(actualValue);
  }

  @Test
  void testPrimaryBuilderPath() {
    JdbcMetastorePersistenceImpl.Builder jdbcMetastorePersistenceServicePrimaryBuilder =
        JdbcMetastorePersistenceImpl.newBuilder(dataSourceMock);
    JdbcMetastorePersistenceImpl jdbcMetastorePersistenceServiceBuilder
        = jdbcMetastorePersistenceServicePrimaryBuilder.build();
    assertNotNull(jdbcMetastorePersistenceServiceBuilder);
  }

}
