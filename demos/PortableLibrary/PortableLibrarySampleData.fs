module PortableLibrarySampleData

let msftCsv = """Date,Open,High,Low,Close,Volume,Adj Close
2012-01-27,29.45,29.53,29.17,29.23,44187700,29.23
2012-01-26,29.61,29.70,29.40,29.50,49102800,29.50
2012-01-25,29.07,29.65,29.07,29.56,59231700,29.56
2012-01-24,29.47,29.57,29.18,29.34,51703300,29.34
2012-01-23,29.55,29.95,29.35,29.73,76078100,29.73
2012-01-20,28.82,29.74,28.75,29.71,165902900,29.71
2012-01-19,28.16,28.44,28.03,28.12,74053500,28.12
2012-01-18,28.31,28.40,27.97,28.23,64860600,28.23
2012-01-17,28.40,28.65,28.17,28.26,72395300,28.26
2012-01-13,27.93,28.25,27.79,28.25,60196100,28.25"""

let authorsXml = """<authors topic="Philosophy of Science">
  <author name="Paul Feyerabend" born="1924" />
  <author name="Thomas Kuhn" />
</authors>"""

let worldBankJson = """
[
    {
        "page": 1,
        "pages": 1,
        "per_page": "1000",
        "total": 53
    },
    [
        {
            "indicator": {
                "id": "GC.DOD.TOTL.GD.ZS",
                "value": "Central government debt, total (% of GDP)"
            },
            "country": {
                "id": "CZ",
                "value": "Czech Republic"
            },
            "value": null,
            "decimal": "1",
            "date": "2012"
        },
        {
            "indicator": {
                "id": "GC.DOD.TOTL.GD.ZS",
                "value": "Central government debt, total (% of GDP)"
            },
            "country": {
                "id": "CZ",
                "value": "Czech Republic"
            },
            "value": null,
            "decimal": "1",
            "date": "2011"
        },
        {
            "indicator": {
                "id": "GC.DOD.TOTL.GD.ZS",
                "value": "Central government debt, total (% of GDP)"
            },
            "country": {
                "id": "CZ",
                "value": "Czech Republic"
            },
            "value": "35.1422970266502",
            "decimal": "1",
            "date": "2010"
        },
        {
            "indicator": {
                "id": "GC.DOD.TOTL.GD.ZS",
                "value": "Central government debt, total (% of GDP)"
            },
            "country": {
                "id": "CZ",
                "value": "Czech Republic"
            },
            "value": null,
            "decimal": "1",
            "date": "1960"
        }
    ]
]"""
