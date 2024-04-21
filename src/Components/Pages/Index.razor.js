function hasFileHandlingSupport() {
    return "launchQueue" in window
}

function processFile(assemblyName, filename, bytes) {
    DotNet.invokeMethodAsync(assemblyName, "ProcessFile", filename, new Uint8Array(bytes));
}

export function setupFileProcessing(assemblyName) {
    if (hasFileHandlingSupport()) {
        launchQueue.setConsumer(async (launchParams) => {
            for (const filehandle of launchParams.files) {
                let file = await filehandle.getFile()
                processFile(assemblyName, file.name, await file.arrayBuffer())
            }
        })
        return true
    }
    return false
}