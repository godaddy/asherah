package com.godaddy.asherah.testapp.rest;

import com.godaddy.asherah.testapp.AppEncryptionTestExecutor;
import com.godaddy.asherah.testapp.configuration.ServerConfiguration;
import com.google.common.collect.ImmutableMap;

import static com.godaddy.asherah.testapp.testhelpers.Constants.*;

import java.time.Instant;
import java.util.Map;
import java.util.concurrent.CompletableFuture;

import javax.ws.rs.DefaultValue;
import javax.ws.rs.GET;
import javax.ws.rs.Path;
import javax.ws.rs.Produces;
import javax.ws.rs.QueryParam;
import javax.ws.rs.core.Context;
import javax.ws.rs.core.MediaType;
import javax.ws.rs.core.Response;
import javax.ws.rs.core.UriInfo;

import org.json.JSONObject;

@Path("/v1/java/appencryption")
@Produces(MediaType.APPLICATION_JSON)
public class AppEncryptionResources {
  private final ServerConfiguration config;
  private final AppEncryptionTestExecutor appEncryptionTestExecutor;

  private static final String PACKAGE_BASE = "com.godaddy.asherah.testapp";
  private static final String PACKAGE_MULTITHREADED = PACKAGE_BASE + ".multithreaded";
  private static final String PACKAGE_REGRESSION = PACKAGE_BASE + ".regression";

  public AppEncryptionResources(final ServerConfiguration config) {
    this.config = config;
    appEncryptionTestExecutor = new AppEncryptionTestExecutor(this.config);
  }

  @GET
  @Path("/multithreaded")
  public String runMultithreadedWriteSuite(@Context final UriInfo uriInfo,
      @QueryParam(TEST_PARAM_NUM_ITERATIONS) @DefaultValue("100") final int numIterations,
      @QueryParam(TEST_PARAM_NUM_REQUESTS) @DefaultValue("100") final int numRequests,
      @QueryParam(TEST_PARAM_NUM_THREADS) @DefaultValue("100") final int numThreads,
      @QueryParam(TEST_PARAM_PAYLOAD_SIZE_BYTES) @DefaultValue("100") final int payloadSizeBytes,
      @QueryParam(TEST_PARAM_THREAD_POOL_TIMEOUT_SECONDS) @DefaultValue("60") final long threadPoolTimeoutSeconds) {
    Map<String, ?> paramMap = ImmutableMap.of(
        TEST_PARAM_NUM_ITERATIONS, numIterations,
        TEST_PARAM_NUM_REQUESTS, numRequests,
        TEST_PARAM_NUM_THREADS, numThreads,
        TEST_PARAM_PAYLOAD_SIZE_BYTES, payloadSizeBytes,
        TEST_PARAM_THREAD_POOL_TIMEOUT_SECONDS, threadPoolTimeoutSeconds);
    return appEncryptionTestExecutor.runPackage(uriInfo.getPath(), PACKAGE_MULTITHREADED, paramMap);
  }

  @GET
  @Path("/async/multithreaded")
  public Response runMultithreadedWriteSuiteAsync(@Context final UriInfo uriInfo,
      @QueryParam(TEST_PARAM_NUM_ITERATIONS) @DefaultValue("100") final int numIterations,
      @QueryParam(TEST_PARAM_NUM_REQUESTS) @DefaultValue("100") final int numRequests,
      @QueryParam(TEST_PARAM_NUM_THREADS) @DefaultValue("100") final int numThreads,
      @QueryParam(TEST_PARAM_PAYLOAD_SIZE_BYTES) @DefaultValue("100") final int payloadSizeBytes,
      @QueryParam(TEST_PARAM_THREAD_POOL_TIMEOUT_SECONDS) @DefaultValue("60") final long threadPoolTimeoutSeconds) {
    // Just submit to common pool and immediately return HTTP 202 Accepted
    CompletableFuture.runAsync(() -> runMultithreadedWriteSuite(uriInfo, numIterations, numRequests, numThreads, payloadSizeBytes,
          threadPoolTimeoutSeconds));
    return Response.accepted(buildAsyncResponse(uriInfo.getPath())).build();
  }

  @GET
  @Path("/regression")
  public String runRegressionTests(@Context final UriInfo uriInfo) {
    return appEncryptionTestExecutor.runPackage(uriInfo.getPath(), PACKAGE_REGRESSION, null);
  }

  private String buildAsyncResponse(final String endpoint) {
    JSONObject response = new JSONObject();
    response.put(TEST_ENDPOINT, endpoint);
    response.put(TEST_EXECUTION_TIMESTAMP, Instant.now().toString());
    return response.toString();
  }

}
