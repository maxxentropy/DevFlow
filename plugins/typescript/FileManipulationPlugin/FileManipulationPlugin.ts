import * as fs from 'fs-extra';
import * as path from 'path';
import { glob } from 'glob';

interface ExecutionContext {
  configuration?: { [key: string]: any };
  inputData?: any;
  workingDirectory: string;
  executionParameters?: { [key: string]: any };
}

interface PluginConfig {
  defaultEncoding: string;
  backupFiles: boolean;
  maxFileSize: number;
  allowedExtensions: string[];
  logLevel: 'debug' | 'info' | 'warn' | 'error';
}

interface FileOperation {
  type: 'read' | 'write' | 'search' | 'transform' | 'list' | 'copy' | 'move' | 'delete';
  filePath?: string;
  pattern?: string;
  content?: string;
  searchTerm?: string;
  replacement?: string;
  destinationPath?: string;
}

interface PluginResult {
  success: boolean;
  message: string;
  data?: any;
  executionTimeMs: number;
  timestamp: string;
  logs: string[];
}

export class FileManipulationPlugin {
  private logs: string[] = [];
  private startTime: Date = new Date();
  private config!: PluginConfig;
  private workingDirectory: string = process.cwd();

  public async executeAsync(context: ExecutionContext): Promise<PluginResult> {
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
    } catch (error: unknown) {
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

  private parseContext(context: ExecutionContext): void {
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

  // --- FIX: Simplified and corrected the operation parsing logic ---
  private getOperation(context: ExecutionContext): FileOperation {
    const inputData = context.inputData || {};
    const operationType = inputData.operation || 'read';

    return {
      type: operationType,
      filePath: inputData.filePath || inputData.file,
      pattern: inputData.pattern,
      content: inputData.content,
      searchTerm: inputData.searchTerm || inputData.search,
      replacement: inputData.replacement || inputData.replace,
      destinationPath: inputData.destinationPath || inputData.destination
    };
  }

  private performOperation(operation: FileOperation): Promise<any> {
    const { type, filePath, content, pattern, searchTerm, replacement, destinationPath } = operation;
    
    // Ensure required parameters exist for each operation type
    switch (type) {
      case 'read':
      case 'delete':
        if (!filePath) throw new Error(`'filePath' is required for a '${type}' operation.`);
        return type === 'read' ? this.readFile(filePath) : this.deleteFile(filePath);
      
      case 'write':
        if (!filePath || content === undefined) throw new Error(`'filePath' and 'content' are required for a 'write' operation.`);
        return this.writeFile(filePath, content);

      case 'transform':
        if (!filePath || searchTerm === undefined || replacement === undefined) throw new Error(`'filePath', 'searchTerm', and 'replacement' are required for a 'transform' operation.`);
        return this.transformFile(filePath, searchTerm, replacement);
        
      case 'search':
        if (!pattern || searchTerm === undefined) throw new Error(`'pattern' and 'searchTerm' are required for a 'search' operation.`);
        return this.searchInFiles(pattern, searchTerm);

      case 'list':
        return this.listFiles(pattern || '**/*');

      case 'copy':
      case 'move':
        if (!filePath || !destinationPath) throw new Error(`'filePath' and 'destinationPath' are required for a '${type}' operation.`);
        return type === 'copy' ? this.copyFile(filePath, destinationPath) : this.moveFile(filePath, destinationPath);

      default:
        throw new Error(`Unsupported operation type: '${type}'`);
    }
  }

  private async readFile(filePath: string): Promise<any> {
    const fullPath = this.resolvePath(filePath);
    const stats = await fs.stat(fullPath);
    if (stats.size > this.config.maxFileSize) throw new Error(`File is too large.`);
    
    const content: string = await fs.readFile(fullPath, { encoding: this.config.defaultEncoding as BufferEncoding });
    this._log(`Read file: ${fullPath} (${stats.size} bytes)`);
    return { filePath: fullPath, content, size: stats.size };
  }

  private async writeFile(filePath: string, content: string): Promise<any> {
    const fullPath = this.resolvePath(filePath);
    if (this.config.backupFiles && await fs.pathExists(fullPath)) {
      await fs.copy(fullPath, `${fullPath}.backup.${Date.now()}`);
    }
    await fs.ensureDir(path.dirname(fullPath));
    await fs.writeFile(fullPath, content, { encoding: this.config.defaultEncoding as BufferEncoding });
    const stats = await fs.stat(fullPath);
    this._log(`Wrote file: ${fullPath} (${stats.size} bytes)`);
    return { filePath: fullPath, size: stats.size };
  }
  
  // ... The rest of the methods (search, transform, list, etc.) are fine ...
  // ... and the helper methods (_log, resolvePath) are also fine ...
  // (Full code for remaining methods for completeness)

  private async searchInFiles(pattern: string, searchTerm: string): Promise<any> {
    const files = await glob(pattern, { cwd: this.workingDirectory, nodir: true, absolute: true });
    const results: { file: string, matches: { lineNumber: number, line: string }[] }[] = [];
    for (const file of files) {
      const stats = await fs.stat(file);
      if (stats.size > this.config.maxFileSize) continue;
      const content: string = await fs.readFile(file, { encoding: this.config.defaultEncoding as BufferEncoding });
      const lines: string[] = content.split('\n');
      const matches: { lineNumber: number, line: string }[] = [];
      lines.forEach((line: string, index: number) => {
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

  private async transformFile(filePath: string, searchTerm: string, replacement: string): Promise<any> {
    const fullPath = this.resolvePath(filePath);
    const originalContent: string = await fs.readFile(fullPath, { encoding: this.config.defaultEncoding as BufferEncoding });
    const transformedContent = originalContent.replace(new RegExp(searchTerm, 'g'), replacement);
    if (this.config.backupFiles) {
      await fs.copy(fullPath, `${fullPath}.backup.${Date.now()}`);
    }
    await fs.writeFile(fullPath, transformedContent, { encoding: this.config.defaultEncoding as BufferEncoding });
    return { filePath: fullPath, originalSize: originalContent.length, newSize: transformedContent.length };
  }

  private async listFiles(pattern: string): Promise<any> {
    const files = await glob(pattern, { cwd: this.workingDirectory, nodir: true, absolute: true });
    return { pattern, totalFiles: files.length, files };
  }

  private async copyFile(source: string, destination: string): Promise<any> {
    const fullSource = this.resolvePath(source);
    const fullDest = this.resolvePath(destination);
    await fs.copy(fullSource, fullDest);
    return { source: fullSource, destination: fullDest };
  }

  private async moveFile(source: string, destination: string): Promise<any> {
    const fullSource = this.resolvePath(source);
    const fullDest = this.resolvePath(destination);
    await fs.move(fullSource, fullDest);
    return { source: fullSource, destination: fullDest };
  }

  private async deleteFile(filePath: string): Promise<any> {
    const fullPath = this.resolvePath(filePath);
    await fs.remove(fullPath);
    return { deletedFile: fullPath };
  }

  private resolvePath(filePath: string): string {
    const resolved = path.resolve(this.workingDirectory, filePath);
    if (!resolved.startsWith(this.workingDirectory)) {
      throw new Error(`Path traversal is not allowed. Attempted to access '${resolved}'.`);
    }
    return resolved;
  }
  
  private _log(message: string): void {
    this.logs.push(message);
    console.error(`[FileManipulation] ${message}`);
  }
}

export default new FileManipulationPlugin();