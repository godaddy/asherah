package memguard_test

import (
	"encoding/base64"
	"fmt"
	"io"
	"os"

	"github.com/godaddy/asherah/go/securememory/memguard"
)

func ExampleSecretFactory_New() {
	factory := new(memguard.SecretFactory)

	secret, err := factory.New([]byte("some really secret value"))
	if err != nil {
		panic("unexpected error!")
	}

	defer secret.Close()

	// do something with the secret...
	fmt.Println(secret.IsClosed())
	// Output: false
}

func ExampleSecretFactory_CreateRandom() {
	factory := new(memguard.SecretFactory)

	secret, err := factory.CreateRandom(32)
	if err != nil {
		panic("unexpected error!")
	}

	defer secret.Close()

	// do something with the secret...
	fmt.Println(secret.IsClosed())
	// Output: false
}

// ExampleWithBytes demonstrates the use of WithBytes to access a secret's protected byte slice.
func ExampleSecretFactory_withBytes() {
	factory := new(memguard.SecretFactory)

	secret, err := factory.CreateRandom(32)
	if err != nil {
		panic("unexpected error!")
	}

	defer secret.Close()

	err = secret.WithBytes(func(bytes []byte) error {
		// You obviously shouldn't ever print a secret but this is just an example
		fmt.Printf("my original secret is %d bytes long", len(bytes))
		return nil
	})
	if err != nil {
		panic("unexpected error!")
	}

	// Output: my original secret is 32 bytes long
}

// ExampleWithBytesFunc demonstrates the use of WithBytesFunc to access a secret's protected byte slice.
func ExampleSecretFactory_withBytesFunc() {
	factory := new(memguard.SecretFactory)

	secret, err := factory.CreateRandom(32)
	if err != nil {
		panic("unexpected error!")
	}

	defer secret.Close()

	// In this example we're encoding our underlying secret data using base64
	encodedBytes, err := secret.WithBytesFunc(func(bytes []byte) ([]byte, error) {
		return []byte(base64.StdEncoding.EncodeToString(bytes)), nil
	})
	if err != nil {
		panic("unexpected error!")
	}

	decodedBytes, err := base64.StdEncoding.DecodeString(string(encodedBytes))
	if err != nil {
		panic("unexpected error!")
	}

	fmt.Printf("my decoded payload is %d bytes long", len(decodedBytes))
	// Output:
	// my decoded payload is 32 bytes long
}

// ExampleNewReader demonstrates working with a secret using the standard io.Reader interface.
func ExampleSecretFactory_newReader() {
	factory := new(memguard.SecretFactory)

	// ignoring errors for simplicity
	s1, _ := factory.New([]byte("first "))
	s2, _ := factory.New([]byte("second "))
	s3, _ := factory.New([]byte("third"))

	defer s1.Close()
	defer s2.Close()
	defer s3.Close()

	r := io.MultiReader(s1.NewReader(), s2.NewReader(), s3.NewReader())

	if _, err := io.Copy(os.Stdout, r); err != nil {
		fmt.Println(err)
	}

	// Output: first second third
}
