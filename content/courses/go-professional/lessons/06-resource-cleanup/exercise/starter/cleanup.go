// Package cleanup opens several resources together and releases them reliably.
package cleanup

import "fmt"

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
			// Bug: resources opened so far are never closed.
			return nil, fmt.Errorf("open %q: %v", name, err)
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
	// Bug: wrong order, stops at the first error, not idempotent.
	for i, r := range g.resources {
		if err := r.Close(); err != nil {
			return fmt.Errorf("close %q: %v", g.names[i], err)
		}
	}
	return nil
}

// Process opens all names, runs work with the resources and always closes
// them afterwards: when work returns, when it returns an error and when it
// panics (the panic must continue after the cleanup). The returned error
// joins the open or work error with any close errors; it is nil only if
// everything succeeded.
func Process(o Opener, names []string, work func([]Resource) error) error {
	g, err := OpenAll(o, names)
	if err != nil {
		return err
	}
	// Bug: cleanup happens only on the happy path and its error is dropped.
	if err := work(g.Resources()); err != nil {
		return err
	}
	_ = g.Close()
	return nil
}
