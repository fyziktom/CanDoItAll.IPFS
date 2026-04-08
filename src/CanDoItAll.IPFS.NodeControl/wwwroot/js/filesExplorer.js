function parseBoolean(value) {
    return String(value).toLowerCase() === "true";
}

function toUploadUrl(endpoint, options) {
    const url = new URL(endpoint, window.location.origin);
    url.searchParams.set("pin", String(Boolean(options.pin)));
    url.searchParams.set("wrap", String(Boolean(options.wrap)));
    return url.toString();
}

function createPickerInput(directory) {
    const input = document.createElement("input");
    input.type = "file";
    input.multiple = true;
    input.style.position = "fixed";
    input.style.left = "-9999px";
    input.style.top = "-9999px";

    if (directory) {
        input.setAttribute("webkitdirectory", "");
        input.setAttribute("directory", "");
        input.webkitdirectory = true;
    }

    document.body.appendChild(input);
    return input;
}

function uniqueDirectories(paths) {
    const directories = new Set();
    for (const path of paths) {
        const segments = path.split("/").filter(Boolean);
        for (let index = 1; index < segments.length; index += 1) {
            directories.add(segments.slice(0, index).join("/"));
        }
    }

    return Array.from(directories).sort((left, right) => left.localeCompare(right));
}

function normalizeRelativePath(path) {
    return String(path || "")
        .replace(/\\/g, "/")
        .split("/")
        .filter((segment) => segment && segment !== "." && segment !== "..")
        .join("/");
}

function buildSelectionFromFileList(fileList) {
    const files = Array.from(fileList || []);
    if (files.length === 0) {
        return null;
    }

    const webkitPaths = files
        .map((file) => normalizeRelativePath(file.webkitRelativePath))
        .filter((path) => path.length > 0);

    if (webkitPaths.length === files.length) {
        const firstSegments = new Set(webkitPaths.map((path) => path.split("/")[0]).filter(Boolean));
        const rootName = firstSegments.size === 1 ? Array.from(firstSegments)[0] : null;
        const relativeFiles = files.map((file, index) => {
            const webkitPath = webkitPaths[index];
            const relativePath = rootName && webkitPath.startsWith(`${rootName}/`)
                ? webkitPath.slice(rootName.length + 1)
                : webkitPath;

            return {
                file,
                relativePath: normalizeRelativePath(relativePath || file.name)
            };
        });

        return {
            rootName,
            directories: uniqueDirectories(relativeFiles.map((entry) => entry.relativePath)),
            files: relativeFiles
        };
    }

    return {
        rootName: null,
        directories: [],
        files: files.map((file) => ({
            file,
            relativePath: normalizeRelativePath(file.name)
        }))
    };
}

async function uploadSelection(selection, options) {
    if (!selection) {
        return null;
    }

    const hasFiles = Array.isArray(selection.files) && selection.files.length > 0;
    const hasDirectories = Array.isArray(selection.directories) && selection.directories.length > 0;
    if (!hasFiles && !hasDirectories && !selection.rootName) {
        return null;
    }

    const formData = new FormData();
    if (selection.rootName) {
        formData.append("rootName", selection.rootName);
    }

    for (const directory of selection.directories || []) {
        formData.append("dir", directory);
    }

    for (const entry of selection.files || []) {
        formData.append("files", entry.file, entry.relativePath);
    }

    const response = await fetch(toUploadUrl(options.endpoint, options), {
        method: "POST",
        body: formData
    });

    if (!response.ok) {
        const message = await response.text();
        throw new Error(message || `Upload failed with status ${response.status}.`);
    }

    return await response.json();
}

async function pickAndUpload(options, directory) {
    return await new Promise((resolve, reject) => {
        const input = createPickerInput(directory);
        let settled = false;
        let focusTimeoutId = null;
        const focusFallbackMs = 10000;

        const cleanup = () => {
            if (focusTimeoutId !== null) {
                window.clearTimeout(focusTimeoutId);
                focusTimeoutId = null;
            }

            input.remove();
            window.removeEventListener("focus", handleFocus, true);
            input.removeEventListener("cancel", handleCancel, true);
        };

        const settle = (callback) => {
            if (settled) {
                return;
            }

            settled = true;
            cleanup();
            callback();
        };

        const handleFocus = () => {
            if (focusTimeoutId !== null) {
                window.clearTimeout(focusTimeoutId);
            }

            focusTimeoutId = window.setTimeout(() => {
                focusTimeoutId = null;
                if (!settled) {
                    const fileCount = input.files ? input.files.length : 0;
                    if (fileCount === 0) {
                        settle(() => resolve(null));
                    }
                }
            }, focusFallbackMs);
        };

        const handleCancel = () => {
            settle(() => resolve(null));
        };

        input.addEventListener("change", async () => {
            settle(async () => {
                try {
                    const selection = buildSelectionFromFileList(input.files);
                    resolve(await uploadSelection(selection, options));
                }
                catch (error) {
                    reject(error);
                }
            });
        }, { once: true });

        window.addEventListener("focus", handleFocus, true);
        input.addEventListener("cancel", handleCancel, { once: true, capture: true });
        input.click();
    });
}

function readDirectoryEntries(entry) {
    return new Promise((resolve, reject) => {
        const reader = entry.createReader();
        const results = [];

        const readNextBatch = () => {
            reader.readEntries((entries) => {
                if (!entries || entries.length === 0) {
                    resolve(results);
                    return;
                }

                results.push(...entries);
                readNextBatch();
            }, reject);
        };

        readNextBatch();
    });
}

function entryToFile(entry) {
    return new Promise((resolve, reject) => {
        entry.file(resolve, reject);
    });
}

async function collectDirectoryChildren(entry, parentPath, directories, files) {
    const children = await readDirectoryEntries(entry);
    if (children.length === 0 && parentPath) {
        directories.add(parentPath);
        return;
    }

    for (const child of children) {
        const childPath = parentPath ? `${parentPath}/${child.name}` : child.name;
        if (child.isDirectory) {
            directories.add(childPath);
            await collectDirectoryChildren(child, childPath, directories, files);
            continue;
        }

        const file = await entryToFile(child);
        files.push({
            file,
            relativePath: normalizeRelativePath(childPath)
        });
    }
}

async function extractSelectionFromEntries(dataTransfer) {
    const rawItems = Array.from(dataTransfer.items || []);
    const rootEntries = rawItems
        .map((item) => typeof item.webkitGetAsEntry === "function" ? item.webkitGetAsEntry() : null)
        .filter(Boolean);

    if (rootEntries.length === 0) {
        return buildSelectionFromFileList(dataTransfer.files);
    }

    const directories = new Set();
    const files = [];

    const singleRootDirectory = rootEntries.length === 1 && rootEntries[0].isDirectory;
    if (singleRootDirectory) {
        await collectDirectoryChildren(rootEntries[0], "", directories, files);
        return {
            rootName: rootEntries[0].name,
            directories: Array.from(directories).sort((left, right) => left.localeCompare(right)),
            files
        };
    }

    for (const entry of rootEntries) {
        if (entry.isDirectory) {
            directories.add(entry.name);
            await collectDirectoryChildren(entry, entry.name, directories, files);
            continue;
        }

        const file = await entryToFile(entry);
        files.push({
            file,
            relativePath: normalizeRelativePath(file.name)
        });
    }

    return {
        rootName: null,
        directories: Array.from(directories).sort((left, right) => left.localeCompare(right)),
        files
    };
}

function readUploadOptionsFromElement(element) {
    return {
        endpoint: element.dataset.uploadEndpoint,
        pin: parseBoolean(element.dataset.uploadPin),
        wrap: parseBoolean(element.dataset.uploadWrap)
    };
}

function setDropZoneState(element, state, enabled) {
    element.classList.toggle(state, enabled);
}

window.filesExplorer = {
    copyText: async function (value) {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            await navigator.clipboard.writeText(value);
            return;
        }

        const fallback = document.createElement("textarea");
        fallback.value = value;
        fallback.setAttribute("readonly", "readonly");
        fallback.style.position = "fixed";
        fallback.style.top = "-9999px";
        document.body.appendChild(fallback);
        fallback.select();
        document.execCommand("copy");
        document.body.removeChild(fallback);
    },

    downloadTextFile: function (fileName, content, contentType) {
        const blob = new Blob([content ?? ""], { type: contentType || "text/plain;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = fileName || "download.txt";
        anchor.style.display = "none";
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
        URL.revokeObjectURL(url);
    },

    pickFilesAndUpload: async function (options) {
        return await pickAndUpload(options, false);
    },

    pickFolderAndUpload: async function (options) {
        return await pickAndUpload(options, true);
    },

    initializeDropZone: function (element, dotNetReference) {
        if (!element || element.__filesExplorerDropZone) {
            return;
        }

        const stop = (event) => {
            event.preventDefault();
            event.stopPropagation();
        };

        const dragEnter = (event) => {
            stop(event);
            setDropZoneState(element, "is-dragging", true);
        };

        const dragLeave = (event) => {
            stop(event);
            if (event.target === element) {
                setDropZoneState(element, "is-dragging", false);
            }
        };

        const dragOver = (event) => {
            stop(event);
            if (event.dataTransfer) {
                event.dataTransfer.dropEffect = "copy";
            }
            setDropZoneState(element, "is-dragging", true);
        };

        const drop = async (event) => {
            stop(event);
            setDropZoneState(element, "is-dragging", false);
            setDropZoneState(element, "is-uploading", true);

            try {
                const selection = await extractSelectionFromEntries(event.dataTransfer);
                const result = await uploadSelection(selection, readUploadOptionsFromElement(element));
                if (result) {
                    await dotNetReference.invokeMethodAsync("HandleDropUploadCompletedAsync", result);
                }
            }
            catch (error) {
                const message = error instanceof Error ? error.message : String(error);
                await dotNetReference.invokeMethodAsync("HandleDropUploadFailedAsync", message);
            }
            finally {
                setDropZoneState(element, "is-uploading", false);
            }
        };

        element.addEventListener("dragenter", dragEnter);
        element.addEventListener("dragleave", dragLeave);
        element.addEventListener("dragover", dragOver);
        element.addEventListener("drop", drop);
        element.__filesExplorerDropZone = {
            dragEnter,
            dragLeave,
            dragOver,
            drop
        };
    }
};
