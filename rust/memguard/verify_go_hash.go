package main

import (
	"crypto/rand"
	"encoding/base64"
	"encoding/hex"
	"fmt"
	"golang.org/x/crypto/blake2b"
)

func Hash(b []byte) []byte {
	h := blake2b.Sum256(b)
	return h[:]
}

func main() {
	inputs := []string{"", "hash", "test"}
	
	for _, input := range inputs {
		hash := Hash([]byte(input))
		fmt.Printf("Input: %q\n", input)
		fmt.Printf("Hash (hex): %s\n", hex.EncodeToString(hash))
		fmt.Printf("Hash (base64): %s\n", base64.StdEncoding.EncodeToString(hash))
		fmt.Printf("Hash (bytes): %v\n\n", hash)
	}
}