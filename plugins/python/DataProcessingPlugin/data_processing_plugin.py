#!/usr/bin/env python3
"""
DataProcessing plugin for DevFlow - provides data manipulation and analysis capabilities.
Supports CSV, JSON, Excel file processing with data validation and transformation.
"""
import sys
import json
import os
import pandas as pd
import numpy as np
from datetime import datetime, timezone
from typing import Dict, Any, List, Optional, Union
from pathlib import Path
import jsonschema
from jsonschema import validate
import shutil

class DataProcessingPlugin:
    """Data processing plugin for DevFlow runtime."""
    
    def __init__(self):
        self.logs = []
        self.start_time = datetime.now(timezone.utc)
        self.config = {}
        self.working_directory = os.getcwd()
    
    async def execute_async(self, context: Dict[str, Any]) -> Dict[str, Any]:
        """Main plugin execution method called by DevFlow runtime."""
        self.start_time = datetime.now(timezone.utc)
        self.logs = []
        
        try:
            self._log('DataProcessing plugin execution started')
            
            # Parse context and configuration
            self._parse_context(context)
            
            # Get the operation to perform
            operation = self._get_operation(context)
            
            self._log(f'Performing {operation["type"]} operation')
            
            # Execute the requested operation
            result = await self._perform_operation(operation)
            
            execution_time = (datetime.now(timezone.utc) - self.start_time).total_seconds() * 1000
            self._log(f'Operation completed in {execution_time:.2f}ms')
            
            return {
                'success': True,
                'message': f'Data operation \'{operation["type"]}\' completed successfully',
                'data': result,
                'executionTimeMs': execution_time,
                'timestamp': datetime.now(timezone.utc).isoformat(),
                'logs': self.logs
            }
            
        except Exception as error:
            execution_time = (datetime.now(timezone.utc) - self.start_time).total_seconds() * 1000
            error_message = str(error)
            self._log(f'Error: {error_message}')
            
            return {
                'success': False,
                'message': f'Data operation failed: {error_message}',
                'executionTimeMs': execution_time,
                'timestamp': datetime.now(timezone.utc).isoformat(),
                'logs': self.logs
            }
    
    def _parse_context(self, context: Dict[str, Any]) -> None:
        """Parse execution context and configuration."""
        # Extract configuration
        self.config = {
            'defaultFormat': 'csv',
            'maxRecords': 100000,
            'dateFormat': 'ISO',
            'encoding': 'utf-8',
            'validateData': True,
            'createBackups': True,
            'logLevel': 'info',
            **(context.get('configuration', {}) or context.get('executionParameters', {}))
        }
        
        # Set working directory
        self.working_directory = context.get('workingDirectory', os.getcwd())
        
        self._log(f'Configuration loaded - Working directory: {self.working_directory}')
        self._log(f'Max records: {self.config["maxRecords"]}')
    
    def _get_operation(self, context: Dict[str, Any]) -> Dict[str, Any]:
        """Extract operation details from context."""
        input_data = context.get('inputData', context)
        
        # Default to reading a CSV file if no specific operation is provided
        if not input_data or not input_data.get('operation'):
            return {
                'type': 'read',
                'filePath': input_data.get('filePath', input_data.get('file', 'data.csv')),
                'format': input_data.get('format', self.config['defaultFormat'])
            }
        
        return {
            'type': input_data.get('operation', 'read'),
            'filePath': input_data.get('filePath', input_data.get('file')),
            'outputPath': input_data.get('outputPath', input_data.get('output')),
            'format': input_data.get('format', self.config['defaultFormat']),
            'schema': input_data.get('schema'),
            'transformations': input_data.get('transformations', []),
            'filters': input_data.get('filters', {}),
            'aggregations': input_data.get('aggregations', {}),
            'data': input_data.get('data')
        }
    
    async def _perform_operation(self, operation: Dict[str, Any]) -> Any:
        """Execute the requested data operation."""
        op_type = operation['type']
        
        if op_type == 'read':
            return await self._read_data(operation['filePath'], operation.get('format'))
        elif op_type == 'write':
            return await self._write_data(operation['data'], operation['outputPath'], operation.get('format'))
        elif op_type == 'transform':
            return await self._transform_data(operation['filePath'], operation['transformations'], operation.get('outputPath'))
        elif op_type == 'validate':
            return await self._validate_data(operation['filePath'], operation.get('schema'))
        elif op_type == 'analyze':
            return await self._analyze_data(operation['filePath'])
        elif op_type == 'filter':
            return await self._filter_data(operation['filePath'], operation['filters'], operation.get('outputPath'))
        elif op_type == 'aggregate':
            return await self._aggregate_data(operation['filePath'], operation['aggregations'], operation.get('outputPath'))
        elif op_type == 'convert':
            return await self._convert_format(operation['filePath'], operation['outputPath'], operation.get('format'))
        else:
            raise ValueError(f'Unsupported operation: {op_type}')
    
    async def _read_data(self, file_path: str, format_type: Optional[str] = None) -> Dict[str, Any]:
        """Read data from file."""
        full_path = os.path.join(self.working_directory, file_path)
        
        if not os.path.exists(full_path):
            raise FileNotFoundError(f'File not found: {full_path}')
        
        # Determine format from extension if not specified
        if not format_type:
            ext = Path(full_path).suffix.lower()
            format_map = {'.csv': 'csv', '.json': 'json', '.xlsx': 'excel', '.xls': 'excel'}
            format_type = format_map.get(ext, 'csv')
        
        self._log(f'Reading {format_type} file: {full_path}')
        
        if format_type == 'csv':
            df = pd.read_csv(full_path, encoding=self.config['encoding'])
        elif format_type == 'json':
            df = pd.read_json(full_path, encoding=self.config['encoding'])
        elif format_type == 'excel':
            df = pd.read_excel(full_path)
        else:
            raise ValueError(f'Unsupported format: {format_type}')
        
        # Check record limit
        if len(df) > self.config['maxRecords']:
            self._log(f'Warning: File has {len(df)} records, truncating to {self.config["maxRecords"]}')
            df = df.head(self.config['maxRecords'])
        
        file_size = os.path.getsize(full_path)
        self._log(f'Read {len(df)} records from {full_path} ({file_size} bytes)')
        
        return {
            'filePath': full_path,
            'format': format_type,
            'records': len(df),
            'columns': list(df.columns),
            'size': file_size,
            'preview': df.head(5).to_dict('records'),
            'dataTypes': {col: str(dtype) for col, dtype in df.dtypes.items()},
            'summary': self._get_summary_stats(df)
        }
    
    async def _write_data(self, data: Any, output_path: str, format_type: Optional[str] = None) -> Dict[str, Any]:
        """Write data to file."""
        full_path = os.path.join(self.working_directory, output_path)
        
        # Create backup if file exists
        if self.config['createBackups'] and os.path.exists(full_path):
            backup_path = f'{full_path}.backup.{int(datetime.now().timestamp())}'
            shutil.copy2(full_path, backup_path)
            self._log(f'Created backup: {backup_path}')
        
        # Convert data to DataFrame if needed
        if isinstance(data, dict):
            df = pd.DataFrame(data)
        elif isinstance(data, list):
            df = pd.DataFrame(data)
        elif isinstance(data, pd.DataFrame):
            df = data
        else:
            raise ValueError('Data must be dict, list, or DataFrame')
        
        # Determine format from extension if not specified
        if not format_type:
            ext = Path(full_path).suffix.lower()
            format_map = {'.csv': 'csv', '.json': 'json', '.xlsx': 'excel'}
            format_type = format_map.get(ext, 'csv')
        
        # Ensure directory exists
        os.makedirs(os.path.dirname(full_path), exist_ok=True)
        
        self._log(f'Writing {len(df)} records to {full_path} as {format_type}')
        
        if format_type == 'csv':
            df.to_csv(full_path, index=False, encoding=self.config['encoding'])
        elif format_type == 'json':
            df.to_json(full_path, orient='records', indent=2)
        elif format_type == 'excel':
            df.to_excel(full_path, index=False)
        else:
            raise ValueError(f'Unsupported format: {format_type}')
        
        file_size = os.path.getsize(full_path)
        self._log(f'Wrote {len(df)} records to {full_path} ({file_size} bytes)')
        
        return {
            'filePath': full_path,
            'format': format_type,
            'records': len(df),
            'size': file_size
        }
    
    async def _transform_data(self, file_path: str, transformations: List[Dict], output_path: Optional[str] = None) -> Dict[str, Any]:
        """Apply transformations to data."""
        full_path = os.path.join(self.working_directory, file_path)
        df = pd.read_csv(full_path, encoding=self.config['encoding'])
        
        original_records = len(df)
        self._log(f'Applying {len(transformations)} transformations to {original_records} records')
        
        for i, transform in enumerate(transformations):
            transform_type = transform.get('type')
            self._log(f'Applying transformation {i+1}: {transform_type}')
            
            if transform_type == 'rename_column':
                df = df.rename(columns={transform['from']: transform['to']})
            elif transform_type == 'filter_rows':
                column = transform['column']
                operator = transform['operator']
                value = transform['value']
                if operator == '==':
                    df = df[df[column] == value]
                elif operator == '!=':
                    df = df[df[column] != value]
                elif operator == '>':
                    df = df[df[column] > value]
                elif operator == '<':
                    df = df[df[column] < value]
            elif transform_type == 'add_column':
                df[transform['name']] = transform.get('value', '')
            elif transform_type == 'remove_column':
                df = df.drop(columns=[transform['column']])
            elif transform_type == 'sort':
                df = df.sort_values(by=transform['column'], ascending=transform.get('ascending', True))
        
        final_records = len(df)
        self._log(f'Transformations complete: {original_records} -> {final_records} records')
        
        result = {
            'originalRecords': original_records,
            'finalRecords': final_records,
            'transformationsApplied': len(transformations),
            'preview': df.head(5).to_dict('records')
        }
        
        if output_path:
            write_result = await self._write_data(df, output_path)
            result.update(write_result)
        
        return result
    
    async def _validate_data(self, file_path: str, schema: Optional[Dict] = None) -> Dict[str, Any]:
        """Validate data against schema."""
        full_path = os.path.join(self.working_directory, file_path)
        df = pd.read_csv(full_path, encoding=self.config['encoding'])
        
        validation_results = {
            'valid': True,
            'errors': [],
            'warnings': [],
            'summary': {
                'totalRecords': len(df),
                'nullValues': df.isnull().sum().sum(),
                'duplicateRows': df.duplicated().sum()
            }
        }
        
        self._log(f'Validating {len(df)} records')
        
        # Basic validation
        if df.empty:
            validation_results['errors'].append('Dataset is empty')
            validation_results['valid'] = False
        
        # Check for null values
        null_counts = df.isnull().sum()
        for col, count in null_counts.items():
            if count > 0:
                validation_results['warnings'].append(f'Column {col} has {count} null values')
        
        # Schema validation if provided
        if schema:
            try:
                data_dict = df.to_dict('records')
                validate(instance=data_dict, schema=schema)
                self._log('Schema validation passed')
            except jsonschema.exceptions.ValidationError as e:
                validation_results['errors'].append(f'Schema validation failed: {e.message}')
                validation_results['valid'] = False
        
        self._log(f'Validation complete: {"PASSED" if validation_results["valid"] else "FAILED"}')
        
        return validation_results
    
    async def _analyze_data(self, file_path: str) -> Dict[str, Any]:
        """Perform statistical analysis on data."""
        full_path = os.path.join(self.working_directory, file_path)
        df = pd.read_csv(full_path, encoding=self.config['encoding'])
        
        self._log(f'Analyzing {len(df)} records with {len(df.columns)} columns')
        
        analysis = {
            'shape': df.shape,
            'columns': list(df.columns),
            'dataTypes': {col: str(dtype) for col, dtype in df.dtypes.items()},
            'summary': self._get_summary_stats(df),
            'nullCounts': df.isnull().sum().to_dict(),
            'duplicateCount': int(df.duplicated().sum()),
            'memoryUsage': df.memory_usage(deep=True).sum()
        }
        
        # Numeric columns analysis
        numeric_cols = df.select_dtypes(include=[np.number]).columns.tolist()
        if numeric_cols:
            analysis['numericAnalysis'] = {
                col: {
                    'mean': float(df[col].mean()),
                    'median': float(df[col].median()),
                    'std': float(df[col].std()),
                    'min': float(df[col].min()),
                    'max': float(df[col].max())
                } for col in numeric_cols
            }
        
        # Categorical columns analysis
        categorical_cols = df.select_dtypes(include=['object']).columns.tolist()
        if categorical_cols:
            analysis['categoricalAnalysis'] = {
                col: {
                    'uniqueValues': int(df[col].nunique()),
                    'mostCommon': df[col].value_counts().head(5).to_dict()
                } for col in categorical_cols
            }
        
        self._log('Data analysis complete')
        
        return analysis
    
    async def _filter_data(self, file_path: str, filters: Dict, output_path: Optional[str] = None) -> Dict[str, Any]:
        """Filter data based on criteria."""
        full_path = os.path.join(self.working_directory, file_path)
        df = pd.read_csv(full_path, encoding=self.config['encoding'])
        
        original_count = len(df)
        self._log(f'Filtering {original_count} records with {len(filters)} filters')
        
        for column, criteria in filters.items():
            if column in df.columns:
                if isinstance(criteria, dict):
                    for operator, value in criteria.items():
                        if operator == 'equals':
                            df = df[df[column] == value]
                        elif operator == 'not_equals':
                            df = df[df[column] != value]
                        elif operator == 'greater_than':
                            df = df[df[column] > value]
                        elif operator == 'less_than':
                            df = df[df[column] < value]
                        elif operator == 'contains':
                            df = df[df[column].str.contains(str(value), na=False)]
                else:
                    df = df[df[column] == criteria]
        
        filtered_count = len(df)
        self._log(f'Filtering complete: {original_count} -> {filtered_count} records')
        
        result = {
            'originalRecords': original_count,
            'filteredRecords': filtered_count,
            'filtersApplied': len(filters),
            'preview': df.head(5).to_dict('records')
        }
        
        if output_path:
            write_result = await self._write_data(df, output_path)
            result.update(write_result)
        
        return result
    
    async def _aggregate_data(self, file_path: str, aggregations: Dict, output_path: Optional[str] = None) -> Dict[str, Any]:
        """Aggregate data using group by operations."""
        full_path = os.path.join(self.working_directory, file_path)
        df = pd.read_csv(full_path, encoding=self.config['encoding'])
        
        group_by = aggregations.get('groupBy', [])
        functions = aggregations.get('functions', {})
        
        if not group_by:
            raise ValueError('groupBy columns must be specified for aggregation')
        
        self._log(f'Aggregating {len(df)} records by {group_by}')
        
        # Perform aggregation
        agg_df = df.groupby(group_by).agg(functions).reset_index()
        
        # Flatten column names if they're multi-level
        if isinstance(agg_df.columns, pd.MultiIndex):
            agg_df.columns = ['_'.join(col).strip() for col in agg_df.columns]
        
        self._log(f'Aggregation complete: {len(df)} -> {len(agg_df)} records')
        
        result = {
            'originalRecords': len(df),
            'aggregatedRecords': len(agg_df),
            'groupByColumns': group_by,
            'aggregationFunctions': functions,
            'preview': agg_df.head(10).to_dict('records')
        }
        
        if output_path:
            write_result = await self._write_data(agg_df, output_path)
            result.update(write_result)
        
        return result
    
    async def _convert_format(self, file_path: str, output_path: str, target_format: str) -> Dict[str, Any]:
        """Convert data between formats."""
        # Read data
        read_result = await self._read_data(file_path)
        
        # Re-read as DataFrame for conversion
        full_path = os.path.join(self.working_directory, file_path)
        df = pd.read_csv(full_path, encoding=self.config['encoding'])
        
        # Write in new format
        write_result = await self._write_data(df, output_path, target_format)
        
        self._log(f'Format conversion complete: {read_result["format"]} -> {target_format}')
        
        return {
            'sourceFormat': read_result['format'],
            'targetFormat': target_format,
            'records': len(df),
            'sourceFile': read_result['filePath'],
            'targetFile': write_result['filePath']
        }
    
    def _get_summary_stats(self, df: pd.DataFrame) -> Dict[str, Any]:
        """Get summary statistics for DataFrame."""
        return {
            'shape': df.shape,
            'columns': len(df.columns),
            'numericColumns': len(df.select_dtypes(include=[np.number]).columns),
            'textColumns': len(df.select_dtypes(include=['object']).columns),
            'nullValues': int(df.isnull().sum().sum()),
            'duplicateRows': int(df.duplicated().sum())
        }
    
    def _log(self, message: str) -> None:
        """Add message to logs."""
        self.logs.append(message)
        if self.config.get('logLevel') in ['debug', 'info']:
            print(f'[DataProcessing] {message}', file=sys.stderr)


# For DevFlow runtime compatibility
if __name__ == '__main__':
    import asyncio
    import sys
    import json
    
    async def main():
        plugin = DataProcessingPlugin()
        
        # Read context from stdin or use default
        if len(sys.argv) > 1:
            context = json.loads(sys.argv[1])
        else:
            context = {
                'inputData': {
                    'operation': 'read',
                    'filePath': 'sample_data.csv'
                }
            }
        
        result = await plugin.execute_async(context)
        print(json.dumps(result, indent=2))
    
    asyncio.run(main())

