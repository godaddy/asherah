package com.godaddy.asherah.testapp;

import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;

import org.junit.jupiter.api.extension.ExtensionContext;
import org.junit.jupiter.api.extension.ParameterContext;
import org.junit.jupiter.api.extension.ParameterResolutionException;
import org.junit.jupiter.api.extension.ParameterResolver;

/**
 * Resolves parameters by checking the {@code ConfigurationParameters} provided via the {@code ExtensionContext}.
 */
public class ConfigurationParameterResolver implements ParameterResolver {
  @Retention(RetentionPolicy.RUNTIME)
  @Target(ElementType.PARAMETER)
  public @interface ConfigurationParameter {
    String value();
  }

  @Override
  public boolean supportsParameter(final ParameterContext parameterContext,
      final ExtensionContext extensionContext) throws ParameterResolutionException {
    // Verify it's annotated w/ our annotation and that the annotation's name appears in the
    // ExtensionContext's configurationParameters
    return parameterContext.findAnnotation(ConfigurationParameter.class)
        .map(ConfigurationParameter::value)
        .map(extensionContext::getConfigurationParameter)
        .isPresent();
  }

  @Override
  public Object resolveParameter(final ParameterContext parameterContext,
      final ExtensionContext extensionContext) throws ParameterResolutionException {
    String paramName = parameterContext.findAnnotation(ConfigurationParameter.class)
        .map(ConfigurationParameter::value)
        .get();
    Class<?> type = parameterContext.getParameter().getType();
    String valueString = extensionContext.getConfigurationParameter(paramName).get();
    return getTypedValue(type, valueString);
  }

  private Object getTypedValue(final Class<?> type, final String valueString) {
    if (byte.class.equals(type)) {
      return Byte.parseByte(valueString);
    }
    if (short.class.equals(type)) {
      return Short.parseShort(valueString);
    }
    if (int.class.equals(type)) {
      return Integer.parseInt(valueString);
    }
    if (long.class.equals(type)) {
      return Long.parseLong(valueString);
    }
    if (float.class.equals(type)) {
      return Float.parseFloat(valueString);
    }
    if (double.class.equals(type)) {
      return Double.parseDouble(valueString);
    }
    if (boolean.class.equals(type)) {
      return Boolean.parseBoolean(valueString);
    }
    if (String.class.equals(type)) {
      return valueString;
    }

    throw new ParameterResolutionException("unhandled type " + type + " for value " + valueString);
  }

}
