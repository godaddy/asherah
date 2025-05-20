#!/bin/bash
cd /Users/jgowdy/asherah/rust/memguard

# Build the test executable
cargo test --lib --no-run 2>&1 | grep -Eo 'target/debug/deps/memguard-[0-9a-f]+' > test_binary.txt
TEST_BINARY=$(cat test_binary.txt)

# Run with abort handler
cat > abort_handler.c << 'EOF'
#include <signal.h>
#include <stdio.h>
#include <stdlib.h>
#include <execinfo.h>
#include <unistd.h>

void abort_handler(int sig) {
    fprintf(stderr, "\n=== SIGABRT CAUGHT IN C HANDLER ===\n");
    fprintf(stderr, "Signal: %d\n", sig);
    
    void *array[100];
    size_t size;
    char **strings;
    
    size = backtrace(array, 100);
    strings = backtrace_symbols(array, size);
    
    fprintf(stderr, "Obtained %zd stack frames.\n", size);
    
    for (size_t i = 0; i < size; i++)
        fprintf(stderr, "%s\n", strings[i]);
    
    free(strings);
    fprintf(stderr, "================================\n\n");
    
    exit(134);
}

int main(int argc, char *argv[]) {
    signal(SIGABRT, abort_handler);
    
    // Run the original program
    execvp(argv[1], argv + 1);
    perror("execvp");
    return 1;
}
EOF

clang -o abort_wrapper abort_handler.c
./abort_wrapper ./"$TEST_BINARY" 2>&1 | tail -100