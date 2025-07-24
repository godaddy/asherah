#!/bin/bash
set -e # Exit on error

echo "Pre-commit dotnet format"

# Check if there are staged changes in AppEncryption directory and format if needed
if git diff --cached --name-only -- csharp/AppEncryption | grep -q .; then
    dotnet format csharp/AppEncryption/AppEncryption.slnx
else
    echo "No changes to csharp/AppEncryption"
fi

# Check if there are staged changes in SecureMemory directory and format if needed
if git diff --cached --name-only -- csharp/SecureMemory | grep -q .; then
    dotnet format csharp/SecureMemory/SecureMemory.slnx
else
    echo "No changes to csharp/SecureMemory"
fi

# Check if there are staged changes in Logging directory and format if needed
if git diff --cached --name-only -- csharp/Logging | grep -q .; then
    dotnet format csharp/Logging/Logging.slnx
else
    echo "No changes to csharp/Logging"
fi
