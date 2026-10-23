// Package cleanup opens several resources together and releases them reliably.
package cleanup

import (
	"errors"
	"fmt"
)

// Resource is anything that must be closed.
type Resource interface {
	Close() error
}

// Opener opens a named resource (a connection, a file, a lock...).
type Opener interface {
	Open(name string) (Resource, error)
}

// Group owns resources opened in order. Close releases them.
//
// Contract:
//   - OpenAll opens names in order. If opening one fails, the resources
//     opened before it are closed in reverse order and the returned error
//     wraps the open failure (message names the resource) together with any
//     errors from closing; no Group is returned.
//   - Close closes the resources in reverse order of opening, attempts every
//     Close even if one fails, and returns all failures joined (each wrapped
//     with the resource name). Calling Close again does nothing and returns
//     nil: every resource is closed exactly once.
type Group struct {
	names     []string
	resources []Resource
}

// OpenAll opens every name using o.
func OpenAll(o Opener, names []string) (*Group, error) {
	g := &Group{}
	for _, name := range names {
		r, err := o.Open(name)
		if err != nil {
			// Undo what has been done so far; keep every error.
			return nil, errors.Join(fmt.Errorf("open %q: %w", name, err), g.Close())
		}
		g.names = append(g.names, name)
		g.resources = append(g.resources, r)
	}
	return g, nil
}

// Resources returns the opened resources in opening order.
func (g *Group) Resources() []Resource {
	return g.resources
}

// Close releases all resources.
func (g *Group) Close() error {
	var errs []error
	for i := len(g.resources) - 1; i >= 0; i-- {
		if err := g.resources[i].Close(); err != nil {
			errs = append(errs, fmt.Errorf("close %q: %w", g.names[i], err))
		}
	}
	// Forget everything: a second Close must not close anything again.
	g.names, g.resources = nil, nil
	return errors.Join(errs...)
}

// Process opens all names, runs work with the resources and always closes
// them afterwards: when work returns, when it returns an error and when it
// panics (the panic must continue after the cleanup). The returned error
// joins the open or work error with any close errors; it is nil only if
// everything succeeded.
func Process(o Opener, names []string, work func([]Resource) error) (err error) {
	g, err := OpenAll(o, names)
	if err != nil {
		return err
	}
	// The deferred call runs on every path, including a panic in work. In the
	// panic case the assignment to err is lost, but the resources are closed
	// and the panic keeps propagating.
	defer func() {
		err = errors.Join(err, g.Close())
	}()
	return work(g.Resources())
}
