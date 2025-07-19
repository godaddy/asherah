#!/bin/bash
echo "Pre-commit dotnet format"

git diff --exit-code --cached --name-only -- csharp/AppEncryption
if [ $? -ne 0 ]; then
    dotnet format csharp/AppEncryption/AppEncryption.slnx
else
    echo "No changes to AppEncryption"
fi

git diff --exit-code --cached --name-only -- csharp/SecureMemory
if [ $? -ne 0 ]; then
    dotnet format csharp/SecureMemory/SecureMemory.slnx
else
    echo "No changes to SecureMemory"
fi

git diff --exit-code --cached --name-only -- csharp/Logging
if [ $? -ne 0 ]; then
    dotnet format csharp/Logging/Logging.slnx
else
    echo "No changes to Logging"
fi
