import { test } from 'node:test';
import assert from 'node:assert/strict';
import { greet } from './main.js';

test('greet builds a greeting', () => {
  assert.equal(greet('Ada'), 'Hello, Ada!');
});

test('greet works with an empty name', () => {
  assert.equal(greet(''), 'Hello, !');
});
