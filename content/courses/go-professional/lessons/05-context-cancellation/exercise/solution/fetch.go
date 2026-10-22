// Package fetch downloads many resources with a bounded number of workers.
package fetch

import (
	"context"
	"fmt"
	"sync"
)

// Fetcher downloads one URL. It must return promptly with ctx.Err() (possibly
// wrapped) once ctx is done.
type Fetcher func(ctx context.Context, url string) (string, error)

// Result is the body downloaded for one URL.
type Result struct {
	URL  string
	Body string
}

// FetchAll downloads all urls using at most `workers` concurrent calls to
// fetch and returns the results in the order of urls.
//
// Required behavior:
//   - if one fetch fails, the others are cancelled, no new fetches are
//     started, and FetchAll returns an error that wraps the failure and names
//     the URL: errors.Is(err, original) must hold;
//   - if ctx is cancelled or its deadline passes, FetchAll returns promptly
//     with an error for which errors.Is(err, ctx.Err()) holds, and does not
//     start new fetches;
//   - when FetchAll returns, none of its goroutines is still running.
func FetchAll(parent context.Context, urls []string, workers int, fetch Fetcher) ([]Result, error) {
	if workers < 1 {
		workers = 1
	}
	ctx, cancel := context.WithCancel(parent)
	defer cancel() // releases the context even on the success path

	results := make([]Result, len(urls))
	jobs := make(chan int)

	var (
		wg       sync.WaitGroup
		mu       sync.Mutex
		firstErr error
	)
	fail := func(err error) {
		mu.Lock()
		if firstErr == nil {
			firstErr = err
		}
		mu.Unlock()
		cancel() // tell the siblings to stop
	}

	for w := 0; w < workers && w < len(urls); w++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			for i := range jobs {
				if ctx.Err() != nil {
					continue // drain without starting new work
				}
				body, err := fetch(ctx, urls[i])
				if err != nil {
					fail(fmt.Errorf("fetch %s: %w", urls[i], err))
					continue
				}
				results[i] = Result{URL: urls[i], Body: body} // distinct index per job
			}
		}()
	}

feed:
	for i := range urls {
		select {
		case jobs <- i:
		case <-ctx.Done():
			break feed
		}
	}
	close(jobs)
	wg.Wait() // all workers have exited when we return

	if err := parent.Err(); err != nil {
		return nil, fmt.Errorf("fetch all: %w", err)
	}
	mu.Lock()
	defer mu.Unlock()
	if firstErr != nil {
		return nil, firstErr
	}
	return results, nil
}
