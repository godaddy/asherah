package com.godaddy.asherah.testapp.configuration;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.godaddy.asherah.testapp.results.ElasticsearchResultExporterImpl;

import io.dropwizard.jackson.Jackson;
import io.dropwizard.jersey.validation.Validators;
import io.dropwizard.configuration.YamlConfigurationFactory;

import org.junit.jupiter.api.Test;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import javax.validation.Validator;
import java.io.File;

import static org.junit.jupiter.api.Assertions.*;

public class ServerConfigurationTest{

    final String TEST_CONFIG = "config.yaml";
    final String TEST_ELASTICSEARCH_URL = "https://elastic.example.com";
    final String TEST_ELASTICSEARCH_PREFIX = "test-prefix";
    final String TEST_KINESIS_STREAM = "example-data-stream";

    private static final Logger LOG
            = LoggerFactory.getLogger(ServerConfiguration.class);
    private YamlConfigurationFactory<ServerConfiguration> configurationFactory;

    public ServerConfigurationTest() {
        final ObjectMapper objectMapper = Jackson.newObjectMapper();
        final Validator validator = Validators.newValidator();
        configurationFactory = new YamlConfigurationFactory<>(ServerConfiguration.class, validator, objectMapper, "dw");

    }

    @Test
    public void getterTest() {
        final File yaml = new File(getClass().getClassLoader().getResource(TEST_CONFIG).getPath());
        try {
            final ServerConfiguration configuration = configurationFactory.build(yaml);
            assertNotNull(configuration);

            String es_prefix = configuration.getElasticsearchIndexPrefix();
            assertNotNull(es_prefix);
            assertEquals(es_prefix,TEST_ELASTICSEARCH_PREFIX);
        } catch (Exception e) {
            LOG.error(e.toString());
            fail("Failed to build ServerConfiguration object");
        }
    }

    @Test
    void testNoExport() {
        final File yaml = new File(getClass().getClassLoader().getResource(TEST_CONFIG).getPath());
        try {
            final ServerConfiguration configuration = configurationFactory.build(yaml);
            assertFalse(configuration.shouldExportResultsToElasticsearch());
            assertFalse(configuration.shouldExportResultsToKinesis());
        } catch (Exception e) {
            LOG.error(e.toString());
            fail("Failed to build ServerConfiguration object");
        }
    }

    @Test
    void testConfigurationConstruction() {
        final File yaml = new File(getClass().getClassLoader().getResource(TEST_CONFIG).getPath());
        try {
            final ServerConfiguration configuration = configurationFactory.build(yaml);
            assertNotNull(configuration);
        } catch (Exception e) {
            LOG.error(e.toString());
            fail("Failed to build ServerConfiguration object");
        }
    }

    @Test
    void testResultExporterConfiguration() {
        final File yaml = new File(getClass().getClassLoader().getResource(TEST_CONFIG).getPath());
        try {
            final ServerConfiguration configuration = configurationFactory.build(yaml);
            ElasticsearchResultExporterImpl resultExporter = new ElasticsearchResultExporterImpl(configuration);
            assertNotNull(resultExporter);

            assertEquals(configuration.getElasticsearchUrl(), TEST_ELASTICSEARCH_URL);
            assertEquals(TEST_ELASTICSEARCH_PREFIX, configuration.getElasticsearchIndexPrefix());

            assertEquals(configuration.getKinesisDataStream(), TEST_KINESIS_STREAM);
        } catch (Exception e) {
            LOG.error(e.toString());
            fail("Failed to build ServerConfiguration object");
        }
    }
}
