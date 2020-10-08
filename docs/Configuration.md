For classes that support IConfiguration based configuration:

## SecureMemory Configuration

<table>
    <tr>
        <th>Configuration Name</th>
        <th>Data type</th>
        <th>Values</th>
        <th>Default / Required</th>
        <th>Description</th>
    </tr>
    <tr>
        <td>secureHeapEngine</td>
        <td>string</td>
        <td>openssl11, (null)</td>
        <td>Platform default</td>
        <td>Controls which secure heap implementation is used</td>
    </tr>
    <tr>
        <td>heapSize</td>
        <td>ulong</td>
        <td>Size in bytes</td>
        <td>32767</td>
        <td>Size of the secure heap in bytes</td>
    </tr>
    <tr>
        <td>minimumAllocationSize</td>
        <td>int</td>
        <td>Size in bytes</td>
        <td>32</td>
        <td>Minimum size of secure heap allocations</td>
    </tr>
    <tr>
        <td>minimumWorkingSetSize</td>
        <td>ulong</td>
        <td>Size in bytes</td>
        <td>33554430</td>
        <td>Windows only: Configure the minimum working set size which influences how much memory can be VirtualLocked</td>
    </tr>
    <tr>
        <td>maximumWorkingSetSize</td>
        <td>ulong</td>
        <td>Size in bytes</td>
        <td>67108860</td>
        <td>Windows only: Configure the maximum working set size which influences how much memory can be VirtualLocked</td>
    </tr>
</table>
<br>
