import http from 'k6/http';
import { sleep } from 'k6';

export const options = {
  iterations: 10,
};

// The default exported function is gonna be picked up by k6 as the entry point for the test script. It will be executed repeatedly in "iterations" for the whole duration of the test.
export default function () {
  // Make a GET request to the target URL
  http.get('http://localhost:5292/v1/update/https%3A%2F%2Fi.siemens.com%2F1P3RW5950-0CH00');
  //http.get('http://localhost:5292/v1/irs/update/aHR0cHM6Ly9zbWFydC5mZXN0by5jb20vYXNzZXQvdHlwZS9vYy9DTU1ULUFTLUMyLTNBLU1QLVMx')
  //http.get('http://localhost:5292/v1/cache/5000');
  // Sleep for 1 second to simulate real-world usage
  sleep(1);
}
