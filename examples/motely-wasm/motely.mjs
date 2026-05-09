// Hand-authored entry point. Wraps the Bootsharp-generated index.mjs to wire
// up Bootsharp.FileSystem before boot so consumers don't have to.
import bootsharp, * as generated from "./index.mjs";
import { init, isFileSystemAvailable } from "@rewaffle/bootsharp-file-system";

const _boot = bootsharp.boot.bind(bootsharp);
bootsharp.boot = async (...args) => {
    if (isFileSystemAvailable())
        init(generated["Bootsharp.FileSystem.FileMounter"]);
    return _boot(...args);
};

export default bootsharp;
export * from "./index.mjs";
export { isFileSystemAvailable };
