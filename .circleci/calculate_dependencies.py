#!/usr/bin/env python3

import os
import sys


if len(sys.argv) <= 2:
    print('Usage: {} <projects_changed_file> <projects_to_build_file>'.format(os.path.basename(__file__)))
    exit(1)

# Dictionary of "<project>=<downstream_dependency1,...>". Ideally this would use language-native dependency tooling
project_dependency_dict = {
    'languages/csharp/Logging': ['languages/csharp/SecureMemory'],
    'languages/csharp/SecureMemory': ['languages/csharp/AppEncryption'],
    'languages/java/secure-memory': ['languages/java/app-encryption'],
    'languages/csharp/AppEncryption': ['samples/csharp/ReferenceApp'],
    'languages/java/app-encryption': ['samples/java/reference-app', 'tests/java/test-app']
}

# Pull in initial known list of changed projects
with open(sys.argv[1]) as projects_changed_file:
    projects_to_build_set = set(projects_changed_file.read().splitlines())

# Loop forever until we're done adding dependencies
while True:
    for project in projects_to_build_set.copy():
        # If this project has downstream dependencies, add them to the set if needed and signal that we need to
        # loop again
        if project_dependency_dict.get(project):
            for dependent in project_dependency_dict[project]:
                if dependent not in projects_to_build_set:
                    projects_to_build_set.add(dependent)
                    added_new_project = True

    # Check if we're looping again or done processing
    if added_new_project:
        added_new_project = False
    else:
        break

# Write final list of projects to build
with open(sys.argv[2], 'w') as outfile:
    for project in projects_to_build_set:
        outfile.write('{}\n'.format(project))
