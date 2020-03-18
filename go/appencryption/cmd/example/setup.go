package main

import (
	"math/rand"
)

const letterBytes = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"

func RandStringBytes(n int) string {
	b := make([]byte, n)
	for i := range b {
		b[i] = letterBytes[rand.Intn(len(letterBytes))]
	}
	return string(b)
}

type Address struct {
	Street string
	City   string
}

func NewAddress() Address {
	return Address{
		Street: RandStringBytes(15),
		City:   RandStringBytes(7),
	}
}

type Contact struct {
	FirstName string    `json:"firstName"`
	LastName  string    `json:"lastName"`
	Addresses []Address `json:"addresses"`
}

func NewContact() Contact {
	return Contact{
		Addresses: []Address{
			NewAddress(),
			NewAddress(),
		},
		LastName:  RandStringBytes(25),
		FirstName: RandStringBytes(8),
	}
}
