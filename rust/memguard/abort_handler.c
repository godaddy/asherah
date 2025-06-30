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
