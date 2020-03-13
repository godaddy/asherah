/*
Package securememory provides a way for applications to keep secret information (like cryptographic keys) in an area of memory
that is secure.

	package main

	import (
		"fmt"

		"github.com/godaddy/asherah/go/securememory/protectedmemory"
	)

	func main() {
		factory := new(protectedmemory.SecretFactory)

		secret, err := factory.New(getSecretFromStore())
		if err != nil {
			panic("unexpected error!")
		}
		defer secret.Close()

		err = secret.WithBytes(func(b []byte) error {
			doSomethingWithSecretBytes(b)
			return nil
		})
		if err != nil {
			panic("unexpected error!")
		}
	}
*/
package securememory
