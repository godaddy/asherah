package com.godaddy.asherah.utils;

import java.time.Instant;
import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.time.format.DateTimeFormatter;

public final class DateTimeUtils {
  private DateTimeUtils() {
  }

  /**
   * Returns current UTC time as {@link java.time.format.DateTimeFormatter#ISO_DATE}.
   * @return
   */
  public static String getCurrentTimeAsUtcIsoDate() {
    return OffsetDateTime.now(ZoneOffset.UTC).format(DateTimeFormatter.ISO_DATE);
  }

  /**
   * Returns current UTC time as {@link java.time.format.DateTimeFormatter#ISO_OFFSET_DATE_TIME}.
   * @return
   */
  public static String getCurrentTimeAsUtcIsoOffsetDateTime() {
    return OffsetDateTime.now(ZoneOffset.UTC).format(DateTimeFormatter.ISO_OFFSET_DATE_TIME);
  }

  /**
   * Returns the UTC time as {@link java.time.format.DateTimeFormatter#ISO_OFFSET_DATE_TIME} for the given {@code instant}.
   * @param instant
   * @return
   */
  public static String getInstantAsUtcIsoOffsetDateTime(final Instant instant) {
    return OffsetDateTime.ofInstant(instant, ZoneOffset.UTC).format(DateTimeFormatter.ISO_OFFSET_DATE_TIME);
  }
}
