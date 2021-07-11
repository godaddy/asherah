<?php
// GENERATED CODE -- DO NOT EDIT!

namespace Asherah\Apps\Server;

/**
 */
class AppEncryptionClient extends \Grpc\BaseStub {

    /**
     * @param string $hostname hostname
     * @param array $opts channel options
     * @param \Grpc\Channel $channel (optional) re-use channel object
     */
    public function __construct($hostname, $opts, $channel = null) {
        parent::__construct($hostname, $opts, $channel);
    }

    /**
     * Performs session operations for a single partition.
     *
     * Each session must begin with a GetSession message with all subsequent
     * Encrypt and Decrypt operations scoped its partition.
     * @param array $metadata metadata
     * @param array $options call options
     */
    public function Session($metadata = [], $options = []) {
        return $this->_bidiRequest('/asherah.apps.server.AppEncryption/Session',
        ['\Asherah\Apps\Server\SessionResponse','decode'],
        $metadata, $options);
    }

}
