<?php
# Generated by the protocol buffer compiler.  DO NOT EDIT!
# source: appencryption.proto

namespace Asherah\Apps\Server;

use Google\Protobuf\Internal\GPBType;
use Google\Protobuf\Internal\RepeatedField;
use Google\Protobuf\Internal\GPBUtil;

/**
 * Generated from protobuf message <code>asherah.apps.server.DecryptResponse</code>
 */
class DecryptResponse extends \Google\Protobuf\Internal\Message
{
    /**
     * Generated from protobuf field <code>bytes data = 1;</code>
     */
    private $data = '';

    public function __construct() {
        \GPBMetadata\Appencryption::initOnce();
        parent::__construct();
    }

    /**
     * Generated from protobuf field <code>bytes data = 1;</code>
     * @return string
     */
    public function getData()
    {
        return $this->data;
    }

    /**
     * Generated from protobuf field <code>bytes data = 1;</code>
     * @param string $var
     * @return $this
     */
    public function setData($var)
    {
        GPBUtil::checkString($var, False);
        $this->data = $var;

        return $this;
    }

}

