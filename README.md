# ZeroEventHub
[![NuGet version (ZeroEventHubClient)](https://img.shields.io/nuget/v/ZeroEventHubClient.svg?style=flat-square)](https://www.nuget.org/packages/ZeroEventHubClient/)

This README file contains information specific to the `dotnet` port of the ZeroEventHub. Please see the [main repository](https://github.com/vippsas/zeroeventhub) for an overview of what this project is about.

## Client

We recommend that you store the latest checkpoint/cursor for each partition in the client's database. Example of simple single-partition consumption. Note about the example:

* Things starting with "my" is supplied by you
* Things starting with "their" is supplied by the service you connect to

```csharp
// Step 1: Setup
const int TheirPartitionCount = 1 //documented contract with server
var requestCallback = new Func<HttpRequestMessage, Task>(message =>
{
    // you can setup the authentication on the request
});
var client = new Client(TheirServerUrl, TheirPartitionCount, requestCallback);

// Step 2: Load the cursors from last time we ran
var cursors = GetMyCursorFromDb();
if (!cursor.Any()){
    // we have never run before, so we can get all events with Cursor.First(0)
    // (if we just want to receive new events from now, we would use Cursor.Last(0))
    cursors = new[] { Cursor.First(0) };
}

// Step 3: Enter listening loop...
while (ShouldContinue)
{
    // Step 4: Use ZeroEventHub client to fetch the next page of events.
    var eventReceiver = EventReceiver<MyDataType>()
    await client.FetchEvents(cursors, MyPageHint, eventReceiver);

    // Step 5: Write the effect of changes to our own database and the updated
    //         cursor value in the same transaction.
    using var transactionScope = new TransactionScope();
    MyWriteEffectOfEventsToDb(eventReceiver.Events);

    cursors = eventReceiver.LatestCheckpoints;
    MyWriteCursorsToDb(cursors);
}
```

## Development

To run the test suite, assuming you already have `dotnet 6` installed and added to your PATH:
```sh
dotnet restore
dotnet test
```

To pass the CI checks, you may need to run the following before pushing your changes:
```sh
dotnet format
```
