// Package fetch downloads many resources with a bounded number of workers.
package fetch

import "context"

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
func FetchAll(ctx context.Context, urls []string, workers int, fetch Fetcher) ([]Result, error) {
	type item struct {
		i    int
		body string
		err  error
	}
	ch := make(chan item) // Bug: unbuffered, and nobody may be listening later
	sem := make(chan struct{}, max(workers, 1))

	for i, u := range urls { // Bug: one goroutine per URL, started before looking at ctx
		go func(i int, u string) {
			sem <- struct{}{}
			defer func() { <-sem }()
			body, err := fetch(ctx, u)
			ch <- item{i, body, err}
		}(i, u)
	}

	results := make([]Result, len(urls))
	for range urls {
		it := <-ch
		if it.err != nil {
			return nil, it.err // Bug: no cancel of the others, no URL in the error, goroutines leak
		}
		results[it.i] = Result{URL: urls[it.i], Body: it.body}
	}
	return results, nil
}
