#!/usr/bin/env python3
"""
ApiIntegration plugin for DevFlow - provides HTTP API integration capabilities.
Supports REST API calls, authentication, webhook handling, and response processing.
"""
import sys
import json
import asyncio
import time
from datetime import datetime, timezone
from typing import Dict, Any, List, Optional, Union
import httpx
import base64
from urllib.parse import urljoin, urlparse
import hashlib
import hmac

class ApiIntegrationPlugin:
    """API integration plugin for DevFlow runtime."""
    
    def __init__(self):
        self.logs = []
        self.start_time = datetime.now(timezone.utc)
        self.config = {}
        self.working_directory = '/tmp'
        self.client = None
    
    async def execute_async(self, context: Dict[str, Any]) -> Dict[str, Any]:
        """Main plugin execution method called by DevFlow runtime."""
        self.start_time = datetime.now(timezone.utc)
        self.logs = []
        
        try:
            self._log('ApiIntegration plugin execution started')
            
            # Parse context and configuration
            self._parse_context(context)
            
            # Initialize HTTP client
            await self._init_client()
            
            # Get the operation to perform
            operation = self._get_operation(context)
            
            self._log(f'Performing {operation["type"]} operation')
            
            # Execute the requested operation
            result = await self._perform_operation(operation)
            
            execution_time = (datetime.now(timezone.utc) - self.start_time).total_seconds() * 1000
            self._log(f'Operation completed in {execution_time:.2f}ms')
            
            return {
                'success': True,
                'message': f'API operation \'{operation["type"]}\' completed successfully',
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
                'message': f'API operation failed: {error_message}',
                'executionTimeMs': execution_time,
                'timestamp': datetime.now(timezone.utc).isoformat(),
                'logs': self.logs
            }
        
        finally:
            if self.client:
                await self.client.aclose()
    
    def _parse_context(self, context: Dict[str, Any]) -> None:
        """Parse execution context and configuration."""
        # Extract configuration
        self.config = {
            'defaultTimeout': 30,
            'maxRetries': 3,
            'retryDelay': 1,
            'followRedirects': True,
            'validateSsl': True,
            'userAgent': 'DevFlow-ApiIntegration/1.0',
            'logLevel': 'info',
            **(context.get('configuration', {}) or context.get('executionParameters', {}))
        }
        
        # Set working directory
        self.working_directory = context.get('workingDirectory', '/tmp')
        
        self._log(f'Configuration loaded - Timeout: {self.config["defaultTimeout"]}s')
        self._log(f'Max retries: {self.config["maxRetries"]}')
    
    async def _init_client(self) -> None:
        """Initialize HTTP client with configuration."""
        self.client = httpx.AsyncClient(
            timeout=self.config['defaultTimeout'],
            follow_redirects=self.config['followRedirects'],
            verify=self.config['validateSsl'],
            headers={'User-Agent': self.config['userAgent']}
        )
        self._log('HTTP client initialized')
    
    def _get_operation(self, context: Dict[str, Any]) -> Dict[str, Any]:
        """Extract operation details from context."""
        input_data = context.get('inputData', context)
        
        # Default to a simple GET request if no specific operation is provided
        if not input_data or not input_data.get('operation'):
            return {
                'type': 'request',
                'method': 'GET',
                'url': input_data.get('url', 'https://httpbin.org/get')
            }
        
        return {
            'type': input_data.get('operation', 'request'),
            'method': input_data.get('method', 'GET'),
            'url': input_data.get('url'),
            'headers': input_data.get('headers', {}),
            'params': input_data.get('params', {}),
            'data': input_data.get('data'),
            'json': input_data.get('json'),
            'auth': input_data.get('auth'),
            'timeout': input_data.get('timeout'),
            'retries': input_data.get('retries'),
            'validateResponse': input_data.get('validateResponse', True),
            'expectedStatus': input_data.get('expectedStatus', [200]),
            'endpoints': input_data.get('endpoints', []),
            'webhook': input_data.get('webhook')
        }
    
    async def _perform_operation(self, operation: Dict[str, Any]) -> Any:
        """Execute the requested API operation."""
        op_type = operation['type']
        
        if op_type == 'request':
            return await self._make_request(operation)
        elif op_type == 'batch_requests':
            return await self._make_batch_requests(operation['endpoints'])
        elif op_type == 'test_endpoint':
            return await self._test_endpoint(operation)
        elif op_type == 'webhook_validate':
            return await self._validate_webhook(operation['webhook'])
        elif op_type == 'health_check':
            return await self._health_check(operation['url'])
        else:
            raise ValueError(f'Unsupported operation: {op_type}')
    
    async def _make_request(self, operation: Dict[str, Any]) -> Dict[str, Any]:
        """Make a single HTTP request without a retry loop."""
        method = operation['method'].upper()
        url = operation['url']
        
        if not url:
            raise ValueError('URL is required for API request')
        
        self._log(f'Making {method} request to {url}')
        
        request_kwargs = {
            'method': method,
            'url': url,
            'headers': operation.get('headers', {}),
            'params': operation.get('params', {})
        }
        
        if operation.get('json'):
            request_kwargs['json'] = operation['json']
        elif operation.get('data'):
            request_kwargs['data'] = operation['data']
            
        if operation.get('auth'):
            request_kwargs['auth'] = self._prepare_auth(operation['auth'])
        
        if operation.get('timeout'):
            request_kwargs['timeout'] = operation['timeout']

        request_start = time.time()
        response = await self.client.request(**request_kwargs)
        request_time = (time.time() - request_start) * 1000
        
        self._log(f'Request completed in {request_time:.2f}ms - Status: {response.status_code}')
        
        if operation.get('validateResponse', True):
            self._validate_response(response, operation.get('expectedStatus', [200]))
        
        result = await self._parse_response(response)
        result['requestTime'] = request_time
        result['attempt'] = 1 # Only one attempt
        
        return result
            
    async def _make_batch_requests(self, endpoints: List[Dict[str, Any]]) -> Dict[str, Any]:
        """Make multiple API requests concurrently."""
        self._log(f'Making batch requests to {len(endpoints)} endpoints')
        
        async def make_single_request(endpoint_config):
            try:
                return await self._make_request(endpoint_config)
            except Exception as e:
                return {
                    'url': endpoint_config.get('url'),
                    'error': str(e),
                    'success': False
                }
        
        # Execute requests concurrently
        batch_start = time.time()
        results = await asyncio.gather(*[make_single_request(endpoint) for endpoint in endpoints])
        batch_time = (time.time() - batch_start) * 1000
        
        # Analyze results
        successful = sum(1 for r in results if r.get('success', True))
        failed = len(results) - successful
        
        self._log(f'Batch completed in {batch_time:.2f}ms - {successful} successful, {failed} failed')
        
        return {
            'totalRequests': len(endpoints),
            'successful': successful,
            'failed': failed,
            'batchTime': batch_time,
            'results': results
        }
    
    async def _test_endpoint(self, operation: Dict[str, Any]) -> Dict[str, Any]:
        """Test an API endpoint comprehensively."""
        url = operation['url']
        self._log(f'Testing endpoint: {url}')
        
        test_results = {
            'url': url,
            'tests': [],
            'overall': 'passed'
        }
        
        # Test 1: Basic connectivity
        try:
            response = await self.client.get(url)
            test_results['tests'].append({
                'name': 'Connectivity',
                'status': 'passed',
                'details': f'Endpoint reachable - Status: {response.status_code}'
            })
        except Exception as e:
            test_results['tests'].append({
                'name': 'Connectivity',
                'status': 'failed',
                'details': f'Endpoint unreachable: {str(e)}'
            })
            test_results['overall'] = 'failed'
        
        # Test 2: Response time
        try:
            start_time = time.time()
            response = await self.client.get(url)
            response_time = (time.time() - start_time) * 1000
            
            if response_time < 1000:  # Less than 1 second
                test_results['tests'].append({
                    'name': 'Response Time',
                    'status': 'passed',
                    'details': f'Response time: {response_time:.2f}ms'
                })
            else:
                test_results['tests'].append({
                    'name': 'Response Time',
                    'status': 'warning',
                    'details': f'Slow response: {response_time:.2f}ms'
                })
        except Exception as e:
            test_results['tests'].append({
                'name': 'Response Time',
                'status': 'failed',
                'details': f'Timeout or error: {str(e)}'
            })
            test_results['overall'] = 'failed'
        
        # Test 3: Response format (if JSON expected)
        try:
            response = await self.client.get(url)
            content_type = response.headers.get('content-type', '')
            
            if 'application/json' in content_type:
                try:
                    response.json()
                    test_results['tests'].append({
                        'name': 'JSON Format',
                        'status': 'passed',
                        'details': 'Valid JSON response'
                    })
                except:
                    test_results['tests'].append({
                        'name': 'JSON Format',
                        'status': 'failed',
                        'details': 'Invalid JSON in response'
                    })
                    test_results['overall'] = 'failed'
            else:
                test_results['tests'].append({
                    'name': 'Content Type',
                    'status': 'info',
                    'details': f'Content-Type: {content_type}'
                })
        except Exception as e:
            test_results['tests'].append({
                'name': 'Response Analysis',
                'status': 'failed',
                'details': f'Error analyzing response: {str(e)}'
            })
        
        self._log(f'Endpoint test completed - Overall: {test_results["overall"]}')
        return test_results
    
    async def _validate_webhook(self, webhook_config: Dict[str, Any]) -> Dict[str, Any]:
        """Validate webhook signature and payload."""
        payload = webhook_config.get('payload')
        signature = webhook_config.get('signature')
        secret = webhook_config.get('secret')
        algorithm = webhook_config.get('algorithm', 'sha256')
        
        self._log('Validating webhook signature')
        
        if not all([payload, signature, secret]):
            raise ValueError('Payload, signature, and secret are required for webhook validation')
        
        # Calculate expected signature
        if isinstance(payload, dict):
            payload_bytes = json.dumps(payload, separators=(',', ':')).encode('utf-8')
        else:
            payload_bytes = str(payload).encode('utf-8')
        
        expected_signature = hmac.new(
            secret.encode('utf-8'),
            payload_bytes,
            getattr(hashlib, algorithm)
        ).hexdigest()
        
        # Compare signatures
        signature_match = hmac.compare_digest(signature, expected_signature)
        
        result = {
            'valid': signature_match,
            'algorithm': algorithm,
            'providedSignature': signature,
            'expectedSignature': expected_signature,
            'payloadSize': len(payload_bytes)
        }
        
        if signature_match:
            self._log('Webhook signature validation passed')
        else:
            self._log('Webhook signature validation failed')
        
        return result
    
    async def _health_check(self, url: str) -> Dict[str, Any]:
        """Perform a comprehensive health check on an API."""
        self._log(f'Performing health check on {url}')
        
        health_data = {
            'url': url,
            'timestamp': datetime.now(timezone.utc).isoformat(),
            'checks': {}
        }
        
        # Check 1: Basic HTTP connectivity
        try:
            start_time = time.time()
            response = await self.client.get(url)
            response_time = (time.time() - start_time) * 1000
            
            health_data['checks']['connectivity'] = {
                'status': 'healthy' if response.status_code < 400 else 'unhealthy',
                'statusCode': response.status_code,
                'responseTime': response_time
            }
        except Exception as e:
            health_data['checks']['connectivity'] = {
                'status': 'unhealthy',
                'error': str(e)
            }
        
        # Check 2: SSL/TLS (for HTTPS URLs)
        if url.startswith('https://'):
            try:
                # This will validate SSL as part of the request
                response = await self.client.get(url)
                health_data['checks']['ssl'] = {
                    'status': 'healthy',
                    'details': 'SSL certificate valid'
                }
            except Exception as e:
                health_data['checks']['ssl'] = {
                    'status': 'unhealthy',
                    'error': str(e)
                }
        
        # Check 3: Response headers
        try:
            response = await self.client.get(url)
            headers = dict(response.headers)
            
            # Check for security headers
            security_headers = [
                'x-frame-options',
                'x-content-type-options',
                'x-xss-protection',
                'strict-transport-security'
            ]
            
            present_security_headers = [h for h in security_headers if h in headers]
            
            health_data['checks']['security_headers'] = {
                'status': 'healthy' if len(present_security_headers) > 2 else 'warning',
                'present': present_security_headers,
                'total': len(security_headers)
            }
        except Exception as e:
            health_data['checks']['security_headers'] = {
                'status': 'unhealthy',
                'error': str(e)
            }
        
        # Determine overall health
        all_checks = health_data['checks'].values()
        healthy_count = sum(1 for check in all_checks if check.get('status') == 'healthy')
        total_checks = len(all_checks)
        
        if healthy_count == total_checks:
            health_data['overall'] = 'healthy'
        elif healthy_count > total_checks / 2:
            health_data['overall'] = 'degraded'
        else:
            health_data['overall'] = 'unhealthy'
        
        self._log(f'Health check completed - Overall: {health_data["overall"]}')
        return health_data
    
    def _prepare_auth(self, auth_config: Dict[str, Any]) -> Union[httpx.BasicAuth, Dict[str, str]]:
        """Prepare authentication for the request."""
        auth_type = auth_config.get('type', 'basic')
        
        if auth_type == 'basic':
            username = auth_config.get('username')
            password = auth_config.get('password')
            if username and password:
                return httpx.BasicAuth(username, password)
        
        elif auth_type == 'bearer':
            token = auth_config.get('token')
            if token:
                return {'Authorization': f'Bearer {token}'}
        
        elif auth_type == 'api_key':
            key = auth_config.get('key')
            header = auth_config.get('header', 'X-API-Key')
            if key:
                return {header: key}
        
        raise ValueError(f'Unsupported auth type: {auth_type}')
    
    def _validate_response(self, response: httpx.Response, expected_status: List[int]) -> None:
        """Validate response status code."""
        if response.status_code not in expected_status:
            raise ValueError(f'Unexpected status code: {response.status_code}, expected one of {expected_status}')
    
    async def _parse_response(self, response: httpx.Response) -> Dict[str, Any]:
        """Parse HTTP response into structured data."""
        result = {
            'status': response.status_code,
            'headers': dict(response.headers),
            'url': str(response.url),
            'success': response.status_code < 400
        }
        
        # Try to parse response body
        content_type = response.headers.get('content-type', '').lower()
        
        try:
            if 'application/json' in content_type:
                result['data'] = response.json()
                result['contentType'] = 'json'
            elif 'text/' in content_type or 'application/xml' in content_type:
                result['data'] = response.text
                result['contentType'] = 'text'
            else:
                result['data'] = base64.b64encode(response.content).decode('utf-8')
                result['contentType'] = 'binary'
                result['size'] = len(response.content)
        except Exception as e:
            result['data'] = f'Error parsing response: {str(e)}'
            result['contentType'] = 'error'
        
        return result
    
    def _log(self, message: str) -> None:
        """Add message to logs."""
        self.logs.append(message)
        if self.config.get('logLevel') in ['debug', 'info']:
            print(f'[ApiIntegration] {message}', file=sys.stderr)


# For DevFlow runtime compatibility
if __name__ == '__main__':
    import asyncio
    import sys
    import json
    
    async def main():
        plugin = ApiIntegrationPlugin()
        
        # Read context from stdin or use default
        if len(sys.argv) > 1:
            context = json.loads(sys.argv[1])
        else:
            context = {
                'inputData': {
                    'operation': 'request',
                    'method': 'GET',
                    'url': 'https://httpbin.org/get'
                }
            }
        
        result = await plugin.execute_async(context)
        print(json.dumps(result, indent=2))
    
    asyncio.run(main())

