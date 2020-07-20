package main

import (
	"encoding/json"
	"fmt"
	"math"
	"os"
	"strconv"
	"strings"
	"text/tabwriter"
	"time"

	"github.com/TylerBrock/colorjson"
	"github.com/logrusorgru/aurora"
	"github.com/rcrowley/go-metrics"
)

var (
	TypeColor  = aurora.White
	TitleColor = aurora.Cyan
	Formatter  = colorjson.NewFormatter()

	w = tabwriter.NewWriter(os.Stderr, 0, 2, 2, ' ', 0)
)

func init() {
	Formatter.Indent = 4
}

func MarshalToMap(obj interface{}) interface{} {
	var b []byte

	switch v := obj.(type) {
	case []byte:
		b = v
	default:
		bytes, err := json.Marshal(obj)
		if err != nil {
			panic(err)
		}
		b = bytes
	}

	var ret interface{}

	if err := json.Unmarshal(b, &ret); err != nil {
		panic(err)
	}

	return ret
}

func PrintColoredJSON(msg string, obj interface{}) {
	obj = MarshalToMap(obj)

	PrintTitle(msg)

	b, err := Formatter.Marshal(obj)
	if err != nil {
		panic(err)
	}

	fmt.Println()
	fmt.Println(string(b))
	fmt.Println()
}

func PrintTitle(name string) {
	fmt.Fprintln(w, aurora.Bold(TitleColor(name)))
}

func Print(name string, v1 interface{}) {
	printRow(TypeColor(name), v1)
}

func printRow(name aurora.Value, v1 interface{}) {
	_, _ = fmt.Fprintf(w, "\t%s\t%v\t\n", name, v1)
}

func printSubRow(name aurora.Value, v1 interface{}) {
	_, _ = fmt.Fprintf(w, "\t  %s\t%v\t\n", name, v1)
}

func PrintPercentiles(t metrics.Timer, p ...float64) {
	Print("Percentiles:", "")

	for _, percentile := range p {
		PrintPercentile(percentile, t)
	}
}

func PrintPercentile(percentile float64, timer metrics.Timer) {
	percentileInt := percentile * 100
	strValue := strconv.FormatFloat(percentileInt, 'f', 2, 64) + "% :"
	printSubRow(aurora.White(strValue), time.Duration(timer.Percentile(percentile)))
}

func PrintRate(t metrics.Timer) {
	Print("Rate:", "")
	printSubRow(aurora.White("1 Minute:"), t.Rate1())
	printSubRow(aurora.White("5 Minute:"), t.Rate5())
	printSubRow(aurora.White("15 Minute:"), t.Rate15())
	printSubRow(aurora.White("Mean:"), t.RateMean())
}

func PrintMetrics(action string, timer metrics.Timer) {
	if timer.Count() == 0 {
		if opts.ShowAll {
			fmt.Println()
			fmt.Println(aurora.Bold(aurora.Cyan(strings.Title(action))), aurora.Red("  Not run."))
		}
		return
	}

	PrintTitle(strings.Title(action))
	Print("Mean:", time.Duration(timer.Mean()))
	Print("Total:", timer.Count())
	Print("Max:", time.Duration(timer.Max()))
	Print("Min:", time.Duration(timer.Min()))
	Print("Variance:", time.Duration(math.Round(timer.Variance()/float64(timer.Count()))))
	PrintRate(timer)
	PrintPercentiles(timer, 0.5, 0.75, 0.8, 0.9, 0.95, 0.99, 0.999)

	w.Flush()

	fmt.Println()
}
