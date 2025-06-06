"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.FileManipulationPlugin = void 0;
const fs = __importStar(require("fs-extra"));
const path = __importStar(require("path"));
const glob_1 = require("glob");
/**
 * FileManipulation plugin for DevFlow - provides file system operations.
 */
class FileManipulationPlugin {
    constructor() {
        this.logs = [];
        this.startTime = new Date();
        this.workingDirectory = process.cwd();
    }
    async executeAsync(context) {
        this.startTime = new Date();
        this.logs = [];
        try {
            this._log('FileManipulation plugin execution started');
            this.parseContext(context);
            const operation = this.getOperation(context);
            this._log(`Performing ${operation.type} operation`);
            const result = await this.performOperation(operation);
            const executionTime = new Date().getTime() - this.startTime.getTime();
            this._log(`Operation completed in ${executionTime}ms`);
            return {
                success: true,
                message: `File operation '${operation.type}' completed successfully`,
                data: result,
                executionTimeMs: executionTime,
                timestamp: new Date().toISOString(),
                logs: this.logs
            };
        }
        catch (error) {
            const executionTime = new Date().getTime() - this.startTime.getTime();
            const errorMessage = error instanceof Error ? error.message : 'Unknown error';
            this._log(`Error: ${errorMessage}`);
            return {
                success: false,
                message: `File operation failed: ${errorMessage}`,
                executionTimeMs: executionTime,
                timestamp: new Date().toISOString(),
                logs: this.logs
            };
        }
    }
    parseContext(context) {
        const providedConfig = { ...context.configuration, ...context.executionParameters };
        this.config = {
            defaultEncoding: 'utf8',
            backupFiles: true,
            maxFileSize: 10485760, // 10MB
            allowedExtensions: ['.txt', '.md', '.json', '.js', '.ts', '.css', '.html'],
            logLevel: 'info',
            ...providedConfig
        };
        this.workingDirectory = path.resolve(context.workingDirectory || process.cwd());
        this._log(`Configuration loaded - Working directory: ${this.workingDirectory}`);
    }
    getOperation(context) {
        const inputData = context.inputData || context;
        return {
            type: inputData.operation || 'read',
            filePath: inputData.filePath || inputData.file,
            pattern: inputData.pattern,
            content: inputData.content,
            searchTerm: inputData.searchTerm || inputData.search,
            replacement: inputData.replacement || inputData.replace,
            destinationPath: inputData.destinationPath || inputData.destination
        };
    }
    performOperation(operation) {
        switch (operation.type) {
            case 'read':
                return this.readFile(operation.filePath);
            case 'write':
                return this.writeFile(operation.filePath, operation.content);
            case 'search':
                return this.searchInFiles(operation.pattern, operation.searchTerm);
            case 'transform':
                return this.transformFile(operation.filePath, operation.searchTerm, operation.replacement);
            case 'list':
                return this.listFiles(operation.pattern || '**/*');
            case 'copy':
                return this.copyFile(operation.filePath, operation.destinationPath);
            case 'move':
                return this.moveFile(operation.filePath, operation.destinationPath);
            case 'delete':
                return this.deleteFile(operation.filePath);
            default:
                throw new Error(`Unsupported operation type: ${operation.type}`);
        }
    }
    async readFile(filePath) {
        const fullPath = this.resolvePath(filePath);
        const stats = await fs.stat(fullPath);
        if (stats.size > this.config.maxFileSize) {
            throw new Error(`File too large: ${stats.size} bytes (max: ${this.config.maxFileSize})`);
        }
        // --- FIX: Pass encoding as an object to resolve overload ambiguity ---
        const content = await fs.readFile(fullPath, { encoding: this.config.defaultEncoding });
        this._log(`Read file: ${fullPath} (${stats.size} bytes)`);
        return { filePath: fullPath, content, size: stats.size };
    }
    async writeFile(filePath, content) {
        const fullPath = this.resolvePath(filePath);
        if (this.config.backupFiles && await fs.pathExists(fullPath)) {
            await fs.copy(fullPath, `${fullPath}.backup.${Date.now()}`);
        }
        await fs.ensureDir(path.dirname(fullPath));
        await fs.writeFile(fullPath, content, { encoding: this.config.defaultEncoding });
        const stats = await fs.stat(fullPath);
        this._log(`Wrote file: ${fullPath} (${stats.size} bytes)`);
        return { filePath: fullPath, size: stats.size };
    }
    async searchInFiles(pattern, searchTerm) {
        const files = await (0, glob_1.glob)(pattern, { cwd: this.workingDirectory, nodir: true, absolute: true });
        const results = [];
        for (const file of files) {
            const stats = await fs.stat(file);
            if (stats.size > this.config.maxFileSize)
                continue;
            // --- FIX: Pass encoding as an object ---
            const content = await fs.readFile(file, { encoding: this.config.defaultEncoding });
            const lines = content.split('\n');
            const matches = [];
            lines.forEach((line, index) => {
                if (line.includes(searchTerm)) {
                    matches.push({ lineNumber: index + 1, line: line.trim() });
                }
            });
            if (matches.length > 0) {
                results.push({ file, matches });
            }
        }
        return { searchTerm, pattern, filesSearched: files.length, results };
    }
    async transformFile(filePath, searchTerm, replacement) {
        const fullPath = this.resolvePath(filePath);
        // --- FIX: Pass encoding as an object ---
        const originalContent = await fs.readFile(fullPath, { encoding: this.config.defaultEncoding });
        const transformedContent = originalContent.replace(new RegExp(searchTerm, 'g'), replacement);
        if (this.config.backupFiles) {
            await fs.copy(fullPath, `${fullPath}.backup.${Date.now()}`);
        }
        await fs.writeFile(fullPath, transformedContent, { encoding: this.config.defaultEncoding });
        return { filePath: fullPath, originalSize: originalContent.length, newSize: transformedContent.length };
    }
    async listFiles(pattern) {
        const files = await (0, glob_1.glob)(pattern, { cwd: this.workingDirectory, nodir: true, absolute: true });
        return { pattern, totalFiles: files.length, files };
    }
    async copyFile(source, destination) {
        const fullSource = this.resolvePath(source);
        const fullDest = this.resolvePath(destination);
        await fs.copy(fullSource, fullDest);
        return { source: fullSource, destination: fullDest };
    }
    async moveFile(source, destination) {
        const fullSource = this.resolvePath(source);
        const fullDest = this.resolvePath(destination);
        await fs.move(fullSource, fullDest);
        return { source: fullSource, destination: fullDest };
    }
    async deleteFile(filePath) {
        const fullPath = this.resolvePath(filePath);
        await fs.remove(fullPath);
        return { deletedFile: fullPath };
    }
    resolvePath(filePath) {
        const resolved = path.resolve(this.workingDirectory, filePath);
        if (!resolved.startsWith(this.workingDirectory)) {
            throw new Error(`Path traversal is not allowed. Attempted to access '${resolved}'.`);
        }
        return resolved;
    }
    _log(message) {
        this.logs.push(message);
        console.error(`[FileManipulation] ${message}`);
    }
}
exports.FileManipulationPlugin = FileManipulationPlugin;
exports.default = new FileManipulationPlugin();
