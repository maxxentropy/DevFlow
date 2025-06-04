import * as fs from 'fs-extra';
import * as path from 'path';
import { glob } from 'glob';

interface PluginContext {
  configuration: { [key: string]: any };
  inputData: any;
  workingDirectory: string;
  executionParameters?: { [key: string]: any };
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
  filesProcessed?: string[];
  executionTimeMs: number;
  timestamp: string;
  logs: string[];
}

/**
 * FileManipulation plugin for DevFlow - provides file system operations.
 * Supports reading, writing, searching, transforming, and managing files.
 */
export class FileManipulationPlugin {
  private logs: string[] = [];
  private startTime: Date = new Date();
  private config: any = {};
  private workingDirectory: string = process.cwd();

  /**
   * Main plugin execution method called by DevFlow runtime.
   */
  public async executeAsync(context: any): Promise<PluginResult> {
    this.startTime = new Date();
    this.logs = [];
    
    try {
      this.log('FileManipulation plugin execution started');
      
      // Parse context and configuration
      this.parseContext(context);
      
      // Get the operation to perform
      const operation = this.getOperation(context);
      
      this.log(`Performing ${operation.type} operation`);
      
      // Execute the requested operation
      const result = await this.performOperation(operation);
      
      const executionTime = new Date().getTime() - this.startTime.getTime();
      this.log(`Operation completed in ${executionTime}ms`);
      
      return {
        success: true,
        message: `File operation '${operation.type}' completed successfully`,
        data: result,
        executionTimeMs: executionTime,
        timestamp: new Date().toISOString(),
        logs: this.logs
      };
      
    } catch (error) {
      const executionTime = new Date().getTime() - this.startTime.getTime();
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      this.log(`Error: ${errorMessage}`);
      
      return {
        success: false,
        message: `File operation failed: ${errorMessage}`,
        executionTimeMs: executionTime,
        timestamp: new Date().toISOString(),
        logs: this.logs
      };
    }
  }

  private parseContext(context: any): void {
    // Extract configuration
    this.config = {
      defaultEncoding: 'utf8',
      backupFiles: true,
      maxFileSize: 10485760, // 10MB
      allowedExtensions: ['.txt', '.md', '.json', '.js', '.ts', '.css', '.html'],
      logLevel: 'info',
      ...((context?.configuration || context?.executionParameters) || {})
    };
    
    // Set working directory
    this.workingDirectory = context?.workingDirectory || process.cwd();
    
    this.log(`Configuration loaded - Working directory: ${this.workingDirectory}`);
    this.log(`Max file size: ${this.config.maxFileSize} bytes`);
  }

  private getOperation(context: any): FileOperation {
    const inputData = context?.inputData || context;
    
    // Default to reading a file if no specific operation is provided
    if (!inputData?.operation) {
      return {
        type: 'read',
        filePath: inputData?.filePath || inputData?.file || 'README.md'
      };
    }
    
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

  private async performOperation(operation: FileOperation): Promise<any> {
    switch (operation.type) {
      case 'read':
        return await this.readFile(operation.filePath!);
      
      case 'write':
        return await this.writeFile(operation.filePath!, operation.content!);
      
      case 'search':
        return await this.searchInFiles(operation.pattern!, operation.searchTerm!);
      
      case 'transform':
        return await this.transformFile(operation.filePath!, operation.searchTerm!, operation.replacement!);
      
      case 'list':
        return await this.listFiles(operation.pattern || '*');
      
      case 'copy':
        return await this.copyFile(operation.filePath!, operation.destinationPath!);
      
      case 'move':
        return await this.moveFile(operation.filePath!, operation.destinationPath!);
      
      case 'delete':
        return await this.deleteFile(operation.filePath!);
      
      default:
        throw new Error(`Unsupported operation: ${operation.type}`);
    }
  }

  private async readFile(filePath: string): Promise<any> {
    const fullPath = path.resolve(this.workingDirectory, filePath);
    this.validateFilePath(fullPath);
    
    const stats = await fs.stat(fullPath);
    if (stats.size > this.config.maxFileSize) {
      throw new Error(`File too large: ${stats.size} bytes (max: ${this.config.maxFileSize})`);
    }
    
    const content = await fs.readFile(fullPath, this.config.defaultEncoding);
    this.log(`Read file: ${fullPath} (${stats.size} bytes)`);
    
    return {
      filePath: fullPath,
      content,
      size: stats.size,
      lastModified: stats.mtime.toISOString(),
      encoding: this.config.defaultEncoding
    };
  }

  private async writeFile(filePath: string, content: string): Promise<any> {
    const fullPath = path.resolve(this.workingDirectory, filePath);
    this.validateFilePath(fullPath);
    
    // Create backup if enabled
    if (this.config.backupFiles && await fs.pathExists(fullPath)) {
      const backupPath = `${fullPath}.backup.${Date.now()}`;
      await fs.copy(fullPath, backupPath);
      this.log(`Created backup: ${backupPath}`);
    }
    
    await fs.ensureDir(path.dirname(fullPath));
    await fs.writeFile(fullPath, content, this.config.defaultEncoding);
    
    const stats = await fs.stat(fullPath);
    this.log(`Wrote file: ${fullPath} (${stats.size} bytes)`);
    
    return {
      filePath: fullPath,
      size: stats.size,
      lastModified: stats.mtime.toISOString(),
      encoding: this.config.defaultEncoding
    };
  }

  private async searchInFiles(pattern: string, searchTerm: string): Promise<any> {
    const searchPath = path.resolve(this.workingDirectory, pattern);
    const files = await glob(searchPath);
    
    const results: any[] = [];
    
    for (const file of files) {
      try {
        const stats = await fs.stat(file);
        if (stats.size > this.config.maxFileSize) continue;
        
        const content = await fs.readFile(file, this.config.defaultEncoding);
        const lines = content.split('\n');
        
        const matches: any[] = [];
        lines.forEach((line, index) => {
          if (line.includes(searchTerm)) {
            matches.push({
              lineNumber: index + 1,
              line: line.trim(),
              context: lines.slice(Math.max(0, index - 1), index + 2)
            });
          }
        });
        
        if (matches.length > 0) {
          results.push({
            file,
            matches,
            totalMatches: matches.length
          });
        }
      } catch (error) {
        this.log(`Skipped file ${file}: ${error}`);
      }
    }
    
    this.log(`Searched ${files.length} files, found matches in ${results.length} files`);
    
    return {
      searchTerm,
      pattern,
      filesSearched: files.length,
      filesWithMatches: results.length,
      results
    };
  }

  private async transformFile(filePath: string, searchTerm: string, replacement: string): Promise<any> {
    const fullPath = path.resolve(this.workingDirectory, filePath);
    this.validateFilePath(fullPath);
    
    const originalContent = await fs.readFile(fullPath, this.config.defaultEncoding);
    const transformedContent = originalContent.replace(new RegExp(searchTerm, 'g'), replacement);
    
    const changes = originalContent.length - transformedContent.length;
    
    if (this.config.backupFiles) {
      const backupPath = `${fullPath}.backup.${Date.now()}`;
      await fs.copy(fullPath, backupPath);
      this.log(`Created backup: ${backupPath}`);
    }
    
    await fs.writeFile(fullPath, transformedContent, this.config.defaultEncoding);
    
    this.log(`Transformed file: ${fullPath} (${Math.abs(changes)} characters ${changes >= 0 ? 'removed' : 'added'})`);
    
    return {
      filePath: fullPath,
      searchTerm,
      replacement,
      charactersChanged: Math.abs(changes),
      originalSize: originalContent.length,
      newSize: transformedContent.length
    };
  }

  private async listFiles(pattern: string): Promise<any> {
    const searchPath = path.resolve(this.workingDirectory, pattern);
    const files = await glob(searchPath);
    
    const fileInfo: any[] = [];
    
    for (const file of files) {
      try {
        const stats = await fs.stat(file);
        fileInfo.push({
          path: file,
          name: path.basename(file),
          size: stats.size,
          lastModified: stats.mtime.toISOString(),
          isDirectory: stats.isDirectory(),
          extension: path.extname(file)
        });
      } catch (error) {
        this.log(`Error reading ${file}: ${error}`);
      }
    }
    
    this.log(`Listed ${fileInfo.length} files matching pattern: ${pattern}`);
    
    return {
      pattern,
      totalFiles: fileInfo.length,
      files: fileInfo
    };
  }

  private async copyFile(sourcePath: string, destinationPath: string): Promise<any> {
    const fullSourcePath = path.resolve(this.workingDirectory, sourcePath);
    const fullDestPath = path.resolve(this.workingDirectory, destinationPath);
    
    this.validateFilePath(fullSourcePath);
    
    await fs.ensureDir(path.dirname(fullDestPath));
    await fs.copy(fullSourcePath, fullDestPath);
    
    const stats = await fs.stat(fullDestPath);
    this.log(`Copied file: ${fullSourcePath} -> ${fullDestPath}`);
    
    return {
      sourcePath: fullSourcePath,
      destinationPath: fullDestPath,
      size: stats.size
    };
  }

  private async moveFile(sourcePath: string, destinationPath: string): Promise<any> {
    const fullSourcePath = path.resolve(this.workingDirectory, sourcePath);
    const fullDestPath = path.resolve(this.workingDirectory, destinationPath);
    
    this.validateFilePath(fullSourcePath);
    
    await fs.ensureDir(path.dirname(fullDestPath));
    await fs.move(fullSourcePath, fullDestPath);
    
    this.log(`Moved file: ${fullSourcePath} -> ${fullDestPath}`);
    
    return {
      sourcePath: fullSourcePath,
      destinationPath: fullDestPath
    };
  }

  private async deleteFile(filePath: string): Promise<any> {
    const fullPath = path.resolve(this.workingDirectory, filePath);
    this.validateFilePath(fullPath);
    
    const stats = await fs.stat(fullPath);
    await fs.remove(fullPath);
    
    this.log(`Deleted file: ${fullPath}`);
    
    return {
      filePath: fullPath,
      deletedSize: stats.size
    };
  }

  private validateFilePath(filePath: string): void {
    if (!filePath || filePath.includes('..')) {
      throw new Error('Invalid file path');
    }
    
    const ext = path.extname(filePath);
    if (this.config.allowedExtensions.length > 0 && !this.config.allowedExtensions.includes(ext)) {
      throw new Error(`File extension ${ext} not allowed`);
    }
  }

  private log(message: string): void {
    this.logs.push(message);
    if (this.config.logLevel === 'debug' || this.config.logLevel === 'info') {
      console.log(`[FileManipulation] ${message}`);
    }
  }
}

// Export for DevFlow runtime
export default FileManipulationPlugin;

