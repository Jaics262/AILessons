const
    fs = require('fs-extra'),
    plumber = require('gulp-plumber'),
    eslint = require('gulp-eslint'),
    changed = require('gulp-changed'),
    del = require('del'),
    nunjucksRender = require('gulp-nunjucks-render'),
    browserSync = require('browser-sync'),
    webpack = require('webpack-stream'),
    svgSprite = require('gulp-svg-sprites'),
    svg2png = require('gulp-svg2png'),
    merge = require('merge-stream'),
    data = require('gulp-data'),
    path = require('path'),
    childProcess = require('child_process'),
    request = require('request'),
    through2 = require("through2"),
    gulp = require('gulp'),
    uglify = require('gulp-uglify'),
    shell = require('gulp-shell'),
    gulpIf = require('gulp-if'),
    dom = require('gulp-dom'),
    htmlmin = require('gulp-htmlmin'),
    fss = require('fs'),
    { execSync } = require('child_process');

require('./gulp/utilsapi');
require('./gulp/website');
  
 
let selenium = null;
let site = null;

function deleteDirectory(inputPath) {
    const folderPath = path.resolve(__dirname, inputPath);
    if (fs.existsSync(folderPath)) {
        fs.rmSync(folderPath, { recursive: true, force: true });
        console.log(`Deleted folder: ${folderPath}`);
    } else {
        console.log(`Folder does not exist: ${folderPath}`);
    }
}
// const fs = require('fs');
// const path = require('path');
gulp.task('resolve:platform', (done) => {
    done();
});
gulp.task('test:html', (done) => {
    done();
});
gulp.task('test:accessibility', (done) => {
    done();
});
gulp.task('test:ui:cucumber', (done) => {
    done();
});
gulp.task('certificates:generate', (done) => {
    done();
});
gulp.task('forms:build', (done) => {
    done();
});
gulp.task('resolve:binaries', (done) => {
    done();
});
gulp.task('db:restore', (done) => {
    done();
});
gulp.task('db:build', (done) => {
    done();
});
gulp.task('db:remove', (done) => {
    done();
});
function isPm2ProcessRunning(processName) {
    try {
        let output = execSync('pm2 jlist', {
            encoding: 'utf8'
        });

        const jsonStart = output.indexOf('[');

        if (jsonStart === -1) {
            console.log('No JSON found in PM2 output');
            return false;
        }

        const processes = JSON.parse(output.substring(jsonStart));

        return processes.some(
            p =>
                p.name?.toLowerCase().includes(processName.toLowerCase()) &&
                p.pm2_env?.status === 'online'
        );
    } catch (err) {
        console.error(err.message);
        return false;
    }
}

function deleteFile(inputPath) {
    const filePath = path.resolve(__dirname, inputPath);

    if (fs.existsSync(filePath)) {
        fs.unlinkSync(filePath);
        console.log(`Deleted file: ${filePath}`);
    } else {
        console.log(`File does not exist: ${filePath}`);
    }
}

function runChildGulpTask(folderName, taskName) {
    const folderPath = path.resolve(__dirname, folderName);

    if (!fs.existsSync(folderPath)) {
        console.log(`Folder does not exist: ${folderPath}`);
        return;
    }

    console.log(`Running '${taskName}' in ${folderPath}`);
    try {
        execSync(`npx gulp ${taskName}`, {
            cwd: folderPath,
            stdio: 'inherit'
        });
    } catch (err) {
        console.error(`Task '${taskName}' failed in ${folderName}. Continuing...`);
        // Ignore error and resume
    }
}

function cleanupPlatform(done) {
    if (isPm2ProcessRunning('cds.identity.provider')) {
        runChildGulpTask('cds.identity.provider', 'deactivate');
    }
    if (isPm2ProcessRunning('cds.identity.manager')) {
        runChildGulpTask('cds.identity.manager', 'deactivate');
    }
    deleteDirectory('cds.identity.provider');
    deleteDirectory('cds.identity.manager');
    deleteFile('cds.identity.provider.zip');
    deleteFile('cds.identity.manager.zip');
    done();
}
gulp.task('refresh:platform', gulp.series(cleanupPlatform, 'resolve:platform'));

gulp.task('generateTestReports', (done) => {//TODO: Depends on Cucumber
    const version = process.argv[3].replace('--version=', '');

    Promise.all([
        generatePdf('cucumber-report/report.html', `sweAutomationReport-${version}.pdf`),
        generatePdf('html-report/report.html', `sweHtmlReport-${version}.pdf`),
        generatePdf('accessibility-report/report.html', `sweAccessibilityReport-${version}.pdf`),
    ]).then(() => {
        try {
            fs.unlinkSync(`sweTestReports-${version}.zip`);
        } catch (e) { }

        return zipDir(`sweTestReports-${version}.zip`, `sweAutomationReport-${version}.pdf`, 1).then(() => {
            return zipDir(`sweTestReports-${version}.zip`, `sweHtmlReport-${version}.pdf`, 1);
        }).then(() => {
            return zipDir(`sweTestReports-${version}.zip`, `sweAccessibilityReport-${version}.pdf`, 1);
        });
    }).then(() => {
        done();
    }).catch((err) => {
        done(err);
    });
});

function generatePdf(file, result) {
    return new Promise((resolve, reject) => {
        //write our stylesheet into the page
        fs.writeFileSync(file, fs.readFileSync(file) + '<style>' + fs.readFileSync('testResults.css') + '</style>');

        const proc = childProcess.spawn('node', [
            'node_modules/electron-pdf/cli.js',
            file,
            result,
            '-w',
            '5000'
        ], {
            cwd: process.cwd(),
            DEBUG: 'electron*'
        });

        proc.on('close', (code) => {
            if (code !== 0) {
                return reject(new Error("PDF generate returned none zero error code"));
            }

            return resolve();
        });
    });
}

function zipDir(file, dir, dontRm) {
    return new Promise((resolve, reject) => {
        if (!dontRm) {
            try {
                fs.removeSync(file);
            } catch (e) {
                console.log(e);
            }
        }

        const proc = childProcess.spawn(_7z, [
            'a',
            file,
            dir
        ]);

        proc.stdout.on('data', (data) => {
            console.log(data.toString());
        });

        proc.stderr.on('data', (data) => {
            console.log(data.toString());
        });

        proc.on('close', (code) => {
            if (code !== 0) {
                return reject(new Error('None zero error code returned'));
            }

            return resolve();
        });
    });
}

function copyConfig() {
    var files = [
        {
            from: "./config/swe.utils.api.web.config",
            to: "./swe.utils.api.build/web.config"
        }
    ];

    return Promise.all(files.map((f) => {
        return new Promise((resolve, reject) => {
            fs.copy(f.from, f.to, (err) => {
                if (err) {
                    return reject(err);
                }

                return resolve();
            });
        });
    }));
}

gulp.task('copyUi', (done) => {
    var files = [
        {
            from: "./swe.website/UI",
            to: "./swe.website/bin/UI"
        }
    ];

    return Promise.all(files.map((f) => {
        return new Promise((resolve, reject) => {
            fs.copy(f.from, f.to, (err) => {
                if (err) {
                    return reject(err);
                }

                return resolve();
            });
        });
    }));
});

gulp.task('prepare', (done) => {
    copyConfig().then(() => {
        return zipDir('swe.website.bin.zip', './swe.website/bin/*');
    }).then(() => {
        return zipDir('cds.identity.manager.bin.zip', './cds.identity.manager/*');
    }).then(() => {
        return zipDir('cds.identity.provider.bin.zip', './cds.identity.provider/*');
    }).then(() => {
        return zipDir('swe.utils.api.bin.zip', './swe.utils.api.build/*');
    }).then(() => {
        return zipDir('swe.utils.sync.bin.zip', './swe.utils.sync.build/*');
    }).then(done).catch(done);
});

gulp.task('nuget:restore', (done) => {
    const proc = childProcess.spawn('dotnet', [
        'restore'
    ], {
        cwd: process.cwd()
    });

    proc.stdout.on('data', (data) => {
        console.log(data.toString());
    });

    proc.stderr.on('data', (data) => {
        console.log(data.toString());
    });

    proc.on('close', (code) => {
        if (code !== 0) {
            return done(new Error('Error carrying out nuget restore'));
        }

        return done();
    });
});

gulp.task('dotnet:restore', (done) => {
    const proc = childProcess.spawn('dotnet', [
        'restore'
    ], {
        cwd: process.cwd()
    });

    proc.stdout.on('data', (data) => {
        console.log(data.toString());
    });

    proc.stderr.on('data', (data) => {
        console.log(data.toString());
    });

    proc.on('close', (code) => {
        if (code !== 0) {
            return done(new Error('Error carrying out dotnet restore'));
        }

        return done();
    });
});

gulp.task('plugins:restore', (done) => {
    const locations = [
        './packages/Diplo.AuditLogViewer/10.1.0/App_Plugins/DiploAuditLogViewer',
        './packages/SimpleRedirects/2.3.0/App_Plugins/SimpleRedirects'
    ];

    const names = [
        'DiploAuditLogViewer',
        'SimpleRedirects'
    ];

    return Promise.all(locations.map((dir, i) => {
        return new Promise((resolve, reject) => {
            fs.copy(dir, path.join('./swe.website/bin/App_Plugins/', names[i]), (err) => {
                if (err) {
                    return reject(err);
                }

                return resolve();
            });
        });
    }));
});

gulp.task('build:msbuild', (done) => {
    const proc = childProcess.spawn('msbuild.exe', [
        'swe.website.sln',
        '-t:rebuild',
        '-p:Configuration=Website'
    ], {
        cwd: process.cwd()
    });

    proc.stdout.on('data', (data) => {
        var re = /SCS\d{4}/
        var match = re.exec(data.toString());

        console.log(data.toString());

        if (match) {
            return done(new Error('Security warning building project'));
        }
    });

    proc.stderr.on('data', (data) => {
        console.log(data.toString());
    });

    proc.on('close', (code) => {
        if (code !== 0) {
            return done(new Error('Error building project'));
        }

        return done();
    });
});

gulp.task("start:site", function (done) {
    childProcess.exec(
        `pm2 start dotnet --name "swe.website"  --interpreter none -- SocialWorkEngland.dll --urls="https://localhost:8881"`,
        { cwd: "swe.website/bin" }, // Ensure the working directory is set to the publish folder
        function (err, stdout, stderr) {
            console.log(stdout);
            console.error(stderr);
            done(err);
        }
    );
});

gulp.task('start:site:old', (done) => {
    const proc = childProcess.spawn('node', [
        './node_modules/pm2/bin/pm2',
        'start',
        'iisexpress.exe',
        '--name=swe.website',
        '--',
        '-port:8881',
        '-path:' + path.join(process.cwd(), 'swe.website\\bin')
    ], {
        cwd: process.cwd()
    });

    proc.stdout.on('data', (data) => {
        console.log(data.toString());
    });

    proc.stderr.on('data', (data) => {
        console.log(data.toString());
    });

    proc.on('close', (code) => {
        if (code !== 0) {
            return done(new Error('None zero error code returned'));
        }

        return done();
    });
});

gulp.task('start:docs', (done) => {
    const proc = childProcess.spawn('node', [
        './node_modules/pm2/bin/pm2',
        'start',
        'iisexpress.exe',
        '--name=swe.docs',
        '--',
        '-port:8080',
        '-path:' + path.join(process.cwd(), 'documents')
    ], {
        cwd: process.cwd()
    });

    proc.stdout.on('data', (data) => {
        console.log(data.toString());
    });

    proc.stderr.on('data', (data) => {
        console.log(data.toString());
    });

    proc.on('close', (code) => {
        if (code !== 0) {
            return done(new Error('None zero error code returned'));
        }

        return done();
    });
});

gulp.task('stop:service', (done) => {
    const proc = childProcess.spawn('node', [
        './node_modules/pm2/bin/pm2',
        'delete',
        process.argv[3].replace('--name=', '')
    ], {
        cwd: process.cwd()
    });

    proc.stdout.on('data', (data) => {
        console.log(data.toString());
    });

    proc.stderr.on('data', (data) => {
        console.log(data.toString());
    });

    proc.on('close', (code) => {
        return done();
    });
});

gulp.task('restart:service', (done) => {
    const proc = childProcess.spawn('node', [
        './node_modules/pm2/bin/pm2',
        'restart',
        process.argv[3].replace('--name=', '')
    ], {
        cwd: process.cwd()
    });

    proc.stdout.on('data', (data) => {
        console.log(data.toString());
    });

    proc.stderr.on('data', (data) => {
        console.log(data.toString());
    });

    proc.on('close', (code) => {
        return done();
    });
});

gulp.task('start:selenium', function (done) {
    const selenium = childProcess.spawn('java', [
        '-jar',
        'selenium.jar',
        'standalone',
        '--port',
        '4444'
    ], {
        cwd: process.cwd(),
        stdio: 'pipe'
    });

    selenium.stdout.on('data', (data) => {
        const text = data.toString();
        //console.log('[SELENIUM]', text);

        // Selenium 4 startup detection
        if (text.includes('Started Selenium Standalone')) {
            console.log('✔ Selenium is UP. Continuing tests...');
            done();
        }
    });

    selenium.stderr.on('data', (data) => {
        console.log('[SELENIUM ERROR]', data.toString());
    });

    selenium.on('exit', (code) => {
        console.log('Selenium exited with code', code);
    });
});

gulp.task('start:papercut', (done) => {
    const proc = childProcess.spawn('./papercut/Papercut.Service.exe', {
        cwd: process.cwd()
    });

    proc.stdout.on('data', (data) => {
        if (data.toString().indexOf('The PapercutServerService service is now running') !== -1) {
            return done();
        }
    });

    proc.on('close', (code) => {
        if (code !== 0) {
            return done(new Error('Failure starting papercut'));
        }

        return done();
    });
});

gulp.task('terminate', (done) => {
    console.log("terminating...");
    setTimeout(() => process.exit(0), 100); // Let stdout flush
    done();
});

const sequenceArgs = (list) => {
    return (done) => {
        const doNext = () => {
            if (list.length === 0) {
                return Promise.resolve();
            }

            const next = list.shift();

            return new Promise((resolve, reject) => {
                const proc = childProcess.spawn('node', [
                    './node_modules/gulp/bin/gulp.js'
                ].concat(next), {
                    cwd: process.cwd()
                });

                proc.stdout.on('data', (data) => {
                    console.log(data.toString().replace(/\n$/, ''));
                });

                proc.stderr.on('data', (data) => {
                    console.log(data.toString().replace(/\n$/, ''));
                });

                proc.on('close', (code) => {
                    if (code !== 0) {
                        reject(new Error('None zero error code returned'));
                        process.exit(code);
                        return;
                    }

                    return resolve();
                });
            }).then(doNext);
        };

        doNext().then(done).catch(done);
    };
}

gulp.task('ensure-site', (done) => {
    const check = () => {
        return new Promise((resolve, reject) => {
            request.get('https://app-swe-uks-dev-umbraco-01.azurewebsites.net/', (err, res, body) => {
                if (!err && res.statusCode === 200) {
                    return resolve(true);
                }

                return resolve(false);
            });
        }).then((res) => {
            if (res) {
                return Promise.resolve();
            }

            return check();
        });
    };

    check().then(done).catch(done);
});

gulp.task('ensure-site-local', (done) => {
    const check = () => {
        return new Promise((resolve, reject) => {
            request.get('https://localhost:8881', (err, res, body) => {
                if (!err && res.statusCode === 200) {
                    return resolve(true);
                }

                return resolve(false);
            });
        }).then((res) => {
            if (res) {
                return Promise.resolve();
            }

            return check();
        });
    };

    check().then(done).catch(done);
});


function generateDocs(schema) {
    return new Promise((resolve, reject) => {
        const proc = childProcess.spawn('node', [
            'generateDBDocs',
            schema
        ], {
            cwd: path.join(process.cwd(), 'documents')
        });

        proc.stdout.on('data', (data) => {
            console.log(data.toString('utf8').replace('\n', ''));
        });

        proc.stderr.on('data', (data) => {
            console.error(data.toString('utf8').replace('\n', ''));
        });

        proc.on('close', (code) => {
            if (code !== 0) {
                return reject(new Error('Error generating docs'));
            }

            return resolve();
        });
    });
}

function generateMssqlDocs(schema) {
    return new Promise((resolve, reject) => {
        const proc = childProcess.spawn('node', [
            'generateMSSQLDocs',
            schema
        ], {
            cwd: path.join(process.cwd(), 'documents')
        });

        proc.stdout.on('data', (data) => {
            console.log(data.toString('utf8').replace('\n', ''));
        });

        proc.stderr.on('data', (data) => {
            console.error(data.toString('utf8').replace('\n', ''));
        });

        proc.on('close', (code) => {
            if (code !== 0) {
                return reject(new Error('Error generating docs'));
            }

            return resolve();
        });
    });
}
gulp.task('build:docs', (done) => {
    generateDocs('swe-edi-db').then(() => {
        return generateDocs('swe-app-db-test');
    }).then(() => {
        return generateMssqlDocs('cds.identity.db');
    }).then(() => {
        return generateMssqlDocs('swe.website.db.test');
    }).then(done).catch(done);
});

gulp.task('indexes:clear', (done) => {
    fs.removeSync(path.join(process.env.APPDATA, '../Local/Temp/ExamineIndexes'));
    fs.removeSync(path.join(process.env.TEMP, 'ExamineIndexes'));

    done();
});

gulp.task('test:reset', sequenceArgs([
    ['db:restore', '--db=swe.website.db.test'],
    ['db:build', '--db=swe-app-db-test'],
    ['db:build', '--db=swe-edi-db'],
    ['db:restore', '--db=cds.identity.db'],
    ['db:build', '--db=cds.identity.db'],
    ['stop:service', '--name=swe.website'],
    ['indexes:clear'],
    ['start:site']
]));

gulp.task('test:local', sequenceArgs([
    ['test:ui:cucumber', '--suite=local'],
]));

gulp.task('test:accept', sequenceArgs([
    ['test:ui:cucumber', '--suite=acceptance'],
]));

gulp.task('test', gulp.series(
    'start:selenium',
    'ensure-site-local',
    'start:papercut',
    'test:local',
    'test:html',
    'test:accessibility',
    'terminate'
));

gulp.task('test:acceptance', gulp.series(
    'start:selenium',
    'ensure-site',
    'test:accept',
    'terminate'
));

gulp.task('activate:reactivate', sequenceArgs([
    ['refresh:platform'],
    ['start:site']
]));

gulp.task('activate:nobuild', sequenceArgs([
    ['certificates:generate'],
    ['db:restore', '--db=swe.website.db.test'],
    ['db:build', '--db=swe-app-db-test'],
    ['db:build', '--db=swe-edi-db'],
    ['db:restore', '--db=cds.identity.db'],
    ['db:build', '--db=cds.identity.db'],
    ['indexes:clear'],
    ['refresh:platform'],
    ['stop:service', '--name=swe.website'],
    ['stop:service', '--name=swe.docs'],
    ['stop:service', '--name=swe.utils.api'],
    ['start:site'],
    ['start:api'],
    ['start:docs']
]));

gulp.task('deactivate', sequenceArgs([
    ['stop:service', '--name=ASPNETCORE_ENVIRONMENT=Development dotnet cds.identity.provider'],
    ['stop:service', '--name=ASPNETCORE_ENVIRONMENT=local dotnet cds.identity.manager'],
    ['stop:service', '--name=swe.website'],
    ['stop:service', '--name=swe.docs'],
    ['stop:service', '--name=swe.utils.api'],
    ['db:remove', '--db=swe.website.db.test'],
    ['db:remove', '--db=swe.website.db'],
    ['db:remove', '--db=swe.identity.db'],
    ['db:remove', '--db=swe-app-db-test']
]));

// set cms watch port number
const iisPort = 17030;

// Generate svg 'sprite'
gulp.task('svgSprite', function () {
    const src = 'swe.website/UI/assets/src/img/svg-icons/**/*.svg';
    const dest = 'swe.website/UI/assets/dist/img/';

    // compile svg files into 'sprite'
    const svg = gulp.src(src)
        .pipe(svgSprite({
            mode: 'symbols',
            common: 'svg-icon',
            svgId: 'icon-%f',
            preview: {
                sprite: 'svg-icons/sprite.html',
                defs: 'svg-icons/defs.html',
                symbols: 'svg-icons/symbols.html',
            },
            svg: {
                sprite: 'svg-icons/sprite.svg',
                defs: 'svg-icons/defs.svg',
                symbols: 'svg-icons/symbols.svg',
            }
        }))
        .pipe(gulp.dest(dest));

    // create png fallbacks
    const png = gulp.src(src)
        .pipe(svg2png())
        .pipe(gulp.dest('swe.website/UI/assets/dist/img/svg-icons/png-fallback/'))

    return merge(svg, png);
});
async function lintmd() {
    const markdownlint = (await import('markdownlint')).default;
    let errTimeout = null;
    let hasError = false;
    return gulp.src("documents/**/*.md", {
        "read": false
    }).pipe(through2.obj(function obj(file, enc, next) {
        markdownlint({
            files: [
                file.path
            ],
            config: {
                line_length: false,
                MD034: false
            }
        },
            function callback(err, result) {
                if (file.path.indexOf('navigation.md') !== -1) {
                    next(err, file);
                    return;
                }

                if (err) {
                    console.log(file.path, err);
                }

                const resultString = (result || "").toString();
                if (resultString) {
                    hasError = true;
                    console.log(file.path, resultString);
                }

                next(err, file);
            }
        );
    })).pipe(through2.obj(function obj(file, enc, next) {
        clearTimeout(errTimeout);
        errTimeout = setTimeout(() => {
            if (hasError) {
                throw new Error('Markdown linting errors detected')
            }
        }, 100);
        next(null);
    }));
}

gulp.task('lintmd', gulp.series(lintmd));

// HTML Validator

gulp.task("validate-all-html", function (done) {

    const files = glob.sync("swe.website/**/*.{html,cshtml}", {
        ignore: [
            "swe.website/**/bin/**",
            "swe.website/**/obj/**",
            "swe.website/**/packages/**",
            "swe.website/**/App_Plugins/**",
            "swe.website/**/node_modules/**",
            "swe.website/**/rendered-html*/**",
            "swe.website/**/dist/**",
            "swe.website/UI/assets/dist/**",
            "swe.website/UI/html/**",
            "swe.website/UI/templates/**"
        ]
    });

    let totalFiles = files.length;
    let filesWithIssues = 0;
    let totalIssues = 0;

    console.log("Validating", totalFiles, "files...\n");

    files.forEach(file => {
        var raw = fss.readFileSync(file, "utf8");
        // AUTO‑FIX: remove trailing spaces and tabs (including EOF lines)
        const cleaned = raw
            .replace(/[ \t]+(?=\r?\n)/g, "") // before newline
            .replace(/[ \t]+$/gm, "");       // end-of-file or final line

        if (cleaned !== raw) {
            fss.writeFileSync(file, cleaned, "utf8");
            raw = cleaned;
        }
        const errors = [];

        // Remove script blocks
        let work = raw.replace(/<script[\s\S]*?<\/script>/gi, "");

        // Escape Razor generics (<T>)
        work = work.replace(/<([A-Za-z0-9_]+)>/g, "##GENERIC_START#$1#GENERIC_END##");

        // Decode HTML entities
        work = work.replace(/&lt;/g, "<")
            .replace(/&gt;/g, ">")
            .replace(/&amp;/g, "&");

        // Restore generics
        work = work.replace(/##GENERIC_START#/g, "&lt;")
            .replace(/#GENERIC_END##/g, "&gt;");

        // IMG ALT VALIDATION
        const imgTags = work.match(/<img[\s\S]*?>/gi) || [];
        imgTags.forEach(tag => {
            const altAttr = tag.match(/alt\s*=\s*"([^"]*)"/i);
            if (!altAttr) {
                errors.push(`Missing alt attribute → ${tag.trim()}`);
                return;
            }
            if (altAttr[1].trim() === "") return; // decorative allowed
        });

        // LINE‑BASED VALIDATIONS
        const lines = work.split(/\r?\n/);

        lines.forEach((line, i) => {
            const lineNum = i + 1;

            // no-redundant-role
            if (/<a[^>]*role="link"/i.test(line)) {
                errors.push(`L${lineNum}: Redundant role="link" on <a>`);
            }

            // void-style
            const voidRegex = /<(meta|img|input|link)\s*\/>/gi;
            let vm;
            while ((vm = voidRegex.exec(line)) !== null) {
                errors.push(`L${lineNum}: Void-style self closing: <${vm[1]}/>`);
            }

            // no-conditional-comment
            if (/<!--\s*\[if.*endif\]\s*-->/i.test(line)) {
                errors.push(`L${lineNum}: Conditional comment`);
            }

            // no-trailing-spaces (ignore blank/indent-only lines)
            if (/[ \t]+$/.test(line) && /[^\s]/.test(line)) {
                errors.push(`L${lineNum}: Trailing whitespace`);
            }
            // no-inline-style
            const inlineStyle = line.match(/\sstyle\s*=\s*"([^"]*)"/i);
            if (inlineStyle && !/=\s*"@/.test(inlineStyle[0])) {
                errors.push(`L${lineNum}: Inline style detected → ${inlineStyle[0].trim()}`);
            }


            // attr-quotes (Razor-safe)
            const safeLine = line.replace(/"[^"]*"/g, "\"\"");
            const tags = safeLine.match(/<[^>]+>/g);
            if (tags) {
                tags.forEach(t => {
                    const uq = t.match(/\s([a-zA-Z0-9\-:]+)=([^\s>"']+)/g);
                    if (uq) {
                        uq.forEach(a => {
                            if (/=\s*@/.test(a)) return;
                            errors.push(`L${lineNum}: Unquoted attribute ${a.trim()}`);
                        });
                    }
                });
            }

            // no-dup-attr
            if (tags) {
                tags.forEach(t => {
                    const names = t.match(/\s([a-zA-Z0-9\-:]+)=/g);
                    if (!names) return;
                    const seen = {};
                    names.forEach(n => {
                        const key = n.trim().replace(/=$/, "");
                        seen[key] = (seen[key] || 0) + 1;
                        if (seen[key] > 1) {
                            errors.push(`L${lineNum}: Duplicate attribute "${key}"`);
                        }
                    });
                });
            }

            // script-type
            if (/<script[^>]*type=["']text\/javascript["']/i.test(line)) {
                errors.push(`L${lineNum}: Unnecessary type="text/javascript" on <script>`);
            }

            // prefer-native-element (fake buttons only)
            if (/<(div|span)[^>]+(onclick=|role="button")/i.test(line)) {
                errors.push(`L${lineNum}: Non-semantic element used as button`);
            }

            // FORM CONTROL ACCESSIBLE NAME
            const fc = line.match(/<(input|textarea|select)([^>]*)>/i);
            if (!fc) return;

            const tag = fc[0];

            if (/type="hidden"/i.test(tag)) return;
            if (/class="[^"]*u-visually-hidden[^"]*"/i.test(tag)) return;

            const pos = work.indexOf(tag);
            const labelStart = work.lastIndexOf("<label", pos);
            const labelEnd = work.indexOf("</label>", pos);
            if (labelStart !== -1 && labelEnd !== -1 && labelEnd > pos) return;

            const idMatch = tag.match(/id="([^"]*)"/i);
            const id = idMatch ? idMatch[1].trim() : null;

            if (id && id.includes("@")) return;

            if (id === "") {
                errors.push(`L${lineNum}: Empty id attribute`);
                return;
            }

            if (id && !/^[A-Za-z][-A-Za-z0-9_:\.\[\]]*$/.test(id)) {
                errors.push(`L${lineNum}: Invalid id "${id}"`);
            }

            if (!id) return;

            const safeId = id.replace(/[\[\]]/g, "\\$&");
            const labelRegex = new RegExp(`<label[^>]*for="[^"]*${safeId}[^"]*"`, "i");

            const hasLabel = labelRegex.test(work);
            const hasAria = /aria-label=|aria-labelledby=|aria-describedby=/i.test(tag);

            if (/type="radio"/i.test(tag)) {
                const fs = work.lastIndexOf("<fieldset", pos);
                const lg = work.indexOf("<legend", fs);
                if (fs !== -1 && lg !== -1) return;
            }

            if (!hasLabel && !hasAria) {
                errors.push(`L${lineNum}: Form control missing accessible name`);
            }
        });

        // GLOBAL VALIDATIONS

        // Only one <main>
        const mains = work.match(/<main/gi) || [];
        if (mains.length > 1) {
            errors.push(`More than one <main> landmark`);
        }

        // long-title
        const titleMatch = work.match(/<title[^>]*>([\s\S]*?)<\/title>/i);
        if (titleMatch) {
            const titleText = titleMatch[1].replace(/\s+/g, " ").trim();
            if (titleText.length > 70) {
                errors.push(`Title too long (${titleText.length} chars)`);
            }
        }

        if (errors.length > 0) {
            filesWithIssues++;
            totalIssues += errors.length;

            console.log(`\nIssues in: ${file}`);
            errors.forEach(e => console.log("   " + e));
        }
    });

    console.log("\n==================================================");
    console.log(`Total files scanned:    ${totalFiles}`);
    console.log(`Files with issues:      ${filesWithIssues}`);
    console.log(`Total issues found:     ${totalIssues}`);
    console.log("==================================================");
    done();
});

///////////////////////////////////////////////////////////////
// JS TASKS
///////////////////////////////////////////////////////////////

// jsTasks uses run-sequence to run globalScripts and linting in sequence

// js linting - config located in .eslintrc
gulp.task('lintjs', function () {
    return gulp.src([
        'swe.website/UI/assets/src/js/project/*'
    ])
        .pipe(eslint({})) // lint the js files for errors
        .pipe(eslint.format('table')) // output results to console

    // callback for run-sequence to work
    return del([BUILD_DIRECTORY]);
});

// used by watch not build
async function globalScripts() {
    const rev = (await import('gulp-rev')).default;

    return gulp.src([
        'swe.website/UI/assets/src/js/project/*'
    ])
        .pipe(gulp.dest('swe.website/UI/assets/dist/js/project/'))
        .pipe(rev())
        .pipe(gulp.dest('swe.website/UI/assets/dist/js/project/'))
        .pipe(uglify())
        .pipe(gulp.dest('swe.website/UI/assets/dist/js/project/'))
        .pipe(rev.manifest('../global-rev-manifest.json'))
        .pipe(gulp.dest('swe.website/UI/assets/dist/js/project/'));

    // callback for run-sequence to work
    return del([BUILD_DIRECTORY]);
}
exports.globalScripts = globalScripts;
gulp.task('globalScripts', globalScripts);


gulp.task('globalVendorScripts', function () {
    return gulp.src([
        'swe.website/UI/assets/src/js/vendor/**'
    ])
        .pipe(gulp.dest('swe.website/UI/assets/dist/js/vendor/'))

    // callback for run-sequence to work
    return del([BUILD_DIRECTORY]);
});
async function typescript() {
    const rev = (await import('gulp-rev')).default;

    return gulp
        .src(['swe.website/UI/assets/src/js/project/ts/root/*.ts'])
        .pipe(plumber())
        .pipe(webpack(webpackConfig))
        .pipe(gulp.dest('swe.website/UI/assets/dist/js/project'))
        .pipe(uglify())
        .pipe(gulp.dest('swe.website/UI/assets/dist/js/project'))
        .pipe(rev())
        .pipe(gulp.dest('swe.website/UI/assets/dist/js/project'))
        .pipe(rev.manifest('../rev-manifest.json'))
        .pipe(gulp.dest('swe.website/UI/assets/dist/js/project'));
}
exports.typescript = this.typescript;
gulp.task('typescript', typescript);

gulp.task('jsTasks', gulp.series('lintjs',
    'globalScripts',
    'globalVendorScripts',
    'typescript')
)
///////////////////////////////////////////////////////////////
// TEMPLATES
///////////////////////////////////////////////////////////////

// create html views using nunjucks templates
gulp.task('templates', function () {
    nunjucksRender.nunjucks.configure(['./swe.website/UI/templates/views/'], { watch: true });
    return gulp.src('./swe.website/UI/templates/views/**/!(_)*.html')
        .pipe(nunjucksRender({
            data: {
                css_path: '/UI/assets/dist/css/',
                js_path: '/UI/assets/dist/js/',
                img_path: '/UI/assets/dist/img/'
            }
        }))
        .pipe(dom(function () {
            const scripts = this.querySelectorAll('script[src]');
            scripts.forEach(script => {
                script.setAttribute('defer', 'defer');
            });
            return this;
        }))
        .pipe(htmlmin({ collapseWhitespace: true }))
        .pipe(gulp.dest('./swe.website/UI/html'));
});

///////////////////////////////////////////////////////////////
// ICON TASKS
///////////////////////////////////////////////////////////////

// copies favicon files to dist
gulp.task('favicons', function () {
    return gulp.src(['swe.website/UI/assets/src/favicons/**.*'], { encoding: false })
        .pipe(gulp.dest('swe.website/UI/assets/dist/favicons/'));
})


///////////////////////////////////////////////////////////////
// IMAGE TASKS
///////////////////////////////////////////////////////////////


// clean task to delete all dist images
gulp.task('clean', function () {
    deleteDirectory('swe.website/UI/assets/dist/**/*');
    deleteDirectory('swe.website/UI/html/**/*');
});

// clean task to delete all dist images
gulp.task('clean', function () {
    return del([
        'swe.website/UI/assets/dist/**/*',
        'swe.website/UI/html/**/*'
    ]);
});

// FONTS

gulp.task('fonts', function () {
    var files = [
        {
            from: "./swe.website/UI/assets/src/fonts/",
            to: "./swe.website/UI/assets/dist/fonts/"
        }
    ];

    return Promise.all(files.map((f) => {
        return new Promise((resolve, reject) => {
            fs.copy(f.from, f.to, (err) => {
                if (err) {
                    return reject(err);
                }

                return resolve();
            });
        });
    }));
});
gulp.task('copyViews', (done) => {
    var files = [
        {
            from: "./swe.website/Views",
            to: "./swe.website/bin/Views"
        }
    ];

    return Promise.all(files.map((f) => {
        return new Promise((resolve, reject) => {
            fs.copy(f.from, f.to, (err) => {
                if (err) {
                    return reject(err);
                }

                return resolve();
            });
        });
    }));
});

// compress images and move to min
async function imgCompress() {
    const imagemin = (await import('gulp-imagemin')).default;
    const pngquant = (await import('imagemin-pngquant')).default;
    function isNotBadGif(file) {
        //console.log(file);
        return !(path.basename(file.path) === 'ajax-loader.gif');
    }
    return gulp.src([
        'swe.website/UI/assets/src/img/**/*.{jpg,png,gif,svg}', '!swe.website/UI/assets/src/img/**/ajax-loader.gif'
    ], { encoding: false })

        .pipe(changed('swe.website/UI/assets/dist/img/'))
        .pipe(gulpIf(isNotBadGif, imagemin({
            progressive: true,
            use: [pngquant()]
        })))
        .pipe(gulp.dest('swe.website/UI/assets/dist/img/'))
        .pipe(browserSync.reload({ stream: true, once: true }));
}

exports.imgCompress = imgCompress;
const replace = require('gulp-replace');

gulp.task('add-defer', function () {
    return gulp.src('./swe.website/**/*.{cshtml,html}', { base: './' })
        .pipe(through2.obj(function (file, enc, cb) {
            const originalContent = file.contents.toString();
            const updatedContent = originalContent.replace(/<script\s+([^>]*?)src=(["'])(.*?)\2([^>]*)>/gi, function (match, before, quote, srcValue, after) {
                if (/async/.test(match)) return match;
                if (/defer/.test(match)) return match;
                if (/jquery/.test(srcValue)) return match;
                if (/tinymce/.test(srcValue)) return match;
                if (/suite/.test(srcValue)) return match;
                if (/CookieProOtSDKStub/.test(srcValue)) return match;
                if (/google/.test(srcValue)) return match;
                if (/appian/.test(srcValue)) return match;
                return `<script ${before}src=${quote}${srcValue}${quote}${after} defer>`;
            });

            if (updatedContent !== originalContent) {
                file.contents = Buffer.from(updatedContent);
                fss.writeFileSync(file.path, file.contents);
            }

            cb(null, file);
        }));
});

///////////////////////////////////////////////////////////////
// WATCH TASKS
///////////////////////////////////////////////////////////////

// This is the watch task. It is called default to ensure that on gulp startup all tasks are run sequentially instead of asynchronously
// defaultTasks are be finished before watch task begins. See defaultTasks for further explaination.
// for all intents and purposes this is still the watch task, only use this for watch functions.
gulp.task('browserSync', function () {
    // This will launch a local server on default port 3000 - http://localhost:3000/
    browserSync.init({
        proxy: "https://localhost:8881"
    });
    gulp.watch('swe.website/UI/assets/src/**/*.scss', gulp.series('sassTasks'));
    gulp.watch(['swe.website/**/*.cs'], gulp.series('rebuild:website'));
    gulp.watch(['swe.website/Views/**/*.cshtml'], gulp.series('copyViews'));
    gulp.watch(['swe.website/UI/assets/src/**/*.js', 'swe.website/UI/assets/src/**/*.ts'], gulp.series('jsTasks', browserSync.reload));
    gulp.watch(['swe.website/UI/templates/**/*.html', 'swe.website/UI/templates/partials/**/*.html'], gulp.series('templates', browserSync.reload));
    gulp.watch(['swe.website/UI/assets/src/img/**/*', '!swe.website/UI/assets/src/img/svg-icons/*'], gulp.series(imgCompress));
    gulp.watch('swe.website/UI/assets/src/favicons/**.*', gulp.series('favicons'));
    gulp.watch(['swe.website/UI/assets/src/img/svg-icons/*'], gulp.series('svgSprite'));
    gulp.watch('swe.website/UI/assets/src/fonts/*', gulp.series('fonts'));
    gulp.watch('./swe.website/**/*.{cshtml,html}', gulp.series('add-defer'));
});
/////////////////////////////////////////////////////////////////
// DEFAULT TASKS
///////////////////////////////////////////////////////////////

// this task used to be called default. It needed to be run in sequence (using runSequence) to improve task running.
// However the watch task cannot be run within runSequence as runSequence stops all tasks at callback
// watch task was renamed default and uses built in gulp function to wait for defaultTasks to finish before starting watch
// for all intents and purposes this is still the default task, just dont include watch here.
gulp.task('defaultTasks',
    gulp.series(imgCompress,
        'favicons',
        'sassTasks',
        'templates',
        'add-defer',
        'copyUi',
        'copyViews',
        'jsTasks',
        'svgSprite',
        'fonts'));
exports['default'] = gulp.series('defaultTasks', 'browserSync');
const retry = async (fn, retries = 3, delay = 500) => {
    let lastError;

    for (let i = 0; i < retries; i++) {
        try {
            return await fn();
        } catch (err) {
            lastError = err;

            if (i < retries - 1) {
                console.log(`Retrying... (${i + 1}/${retries})`);
                await new Promise(resolve => setTimeout(resolve, delay));
            }
        }
    }

    throw lastError;
};
// Define the task to delete all specified folders
gulp.task("clean:website", (done) => {
    // Adjust the path as needed
    console.log('Deleing folders and files from website bin path ');

    const deleteWithRetry = async (path) => {
        try {
            await retry(() => deleteDirectory(path), 3, 500);
            console.log(`Deleted: ${path}`);
        } catch (err) {
            console.error(`Failed to delete ${path}`, err);
        }
    };


    deleteWithRetry('swe.website/bin');
    deleteWithRetry('swe.website/bin');
    deleteWithRetry('swe.website/wwwroot/umbraco');
    deleteWithRetry('swe.website/wwwroot/usync');
    deleteWithRetry('swe.website/wwwroot/uSyncHistory');

    done();
    // Delete matching files and folders
    console.log('Deleing folders and files from website bin path completed');
});


//Run this script if you are running website through Visual studio
gulp.task('copyadditionalscript', function (done) {
    createdirectory('./swe.website/wwwroot/umbraco/');
    createdirectory('./swe.website/wwwroot/App_Plugins/usync');
    createdirectory('./swe.website/wwwroot/App_Plugins/uSyncHistory');
    return copyadditionalscript(done);
});
// Custom wrapper task to run the external MySQL gulpfile
gulp.task('install-mysql', function (done) {
    return shell.task([
        'gulp install-mysql --gulpfile gulpfile.mysql.js'
    ])();
});

function copyadditionalscript(done) {
    var files = [
        {
            from: "./packages/cms17StaticAssets/Assets/umbraco",
            to: "./swe.website/wwwroot/umbraco/"
        },
        {
            from: "./packages/usync/17.3.5",
            to: "./swe.website/wwwroot/App_Plugins/usync"
        },
        {
            from: "./packages/uSyncHistory/17.3.5",
            to: "./swe.website/wwwroot/App_Plugins/uSyncHistory"
        }
    ];

    return Promise.all(files.map((f) => {
        return new Promise((resolve, reject) => {
            fs.copy(f.from, f.to, (err) => {
                if (err) {
                    return reject(err);
                }

                return resolve();
            });
        });
    }));
}
function createdirectory(arg) {

    const dirPath = path.join(__dirname, arg);

    if (!fs.existsSync(dirPath)) {
        fs.mkdirSync(dirPath, { recursive: true });
        console.log('Directory created:', dirPath);
    } else {
        console.log('Directory already exists:', dirPath);
    }
}
//packages\umbraco.cms.staticassets\13.7.0\staticwebassets

//Add defer to script tags


gulp.task('copyViews', (done) => {
    var files = [
        {
            from: "./swe.website/Views",
            to: "./swe.website/bin/Views"
        }
    ];

    return Promise.all(files.map((f) => {
        return new Promise((resolve, reject) => {
            fs.copy(f.from, f.to, (err) => {
                if (err) {
                    return reject(err);
                }

                return resolve();
            });
        });
    }));
});
gulp.task('dbBuild', sequenceArgs([
    ['db:build', '--db=swe-app-db-test'],
    ['db:restore', '--db=swe.website.db.test'],
    ['db:build', '--db=swe-edi-db'],
    ['db:restore', '--db=cds.identity.db'],
    ['db:build', '--db=cds.identity.db']]));
gulp.task('certificate', gulp.series('certificates:generate'));
gulp.task('builddocs', sequenceArgs([
    ['stop:service', '--name=swe.docs'],
    ['build:docs'],
    ['start:docs']]
));
gulp.task('subbuild', gulp.series(
    'nuget:restore',
    'dotnet:restore',
    'plugins:restore',
    'build:msbuild',
    'clean:website',
    imgCompress,
    'build:website',
    'lintmd',
    'favicons',
    'sassTasks:build',
    'jsTasks',
    'svgSprite',
    'templates',
    'fonts',
    'forms:build',
    'copyUi'));

gulp.task('build', gulp.series(
    'subbuild',
    'terminate'));
gulp.task('dbBuild', sequenceArgs([
    ['db:build', '--db=swe-app-db-test'],
    ['db:restore', '--db=swe.website.db.test'],
    ['db:build', '--db=swe-edi-db'],
    ['db:restore', '--db=cds.identity.db'],
    ['db:build', '--db=cds.identity.db']]));
gulp.task('certificate', gulp.series('certificates:generate'));
gulp.task('builddocs', sequenceArgs([
    ['stop:service', '--name=swe.docs'],
    ['build:docs'],
    ['start:docs']]
));

gulp.task('stopService', function (done) {
    childProcess.exec('pm2 delete swe.website', () => done());
});


gulp.task('buildsite', gulp.series(
    'resolve:binaries',
    'stopService',
    'stopapi',
    'subbuild',
    'refresh:platform',
    'indexes:clear',
    'start:site',
    'start:api'
));
gulp.task('activate',
    gulp.series(gulp.parallel('certificate', 'dbBuild', 'buildsite'), 'builddocs',
        'terminate'));

gulp.task('activate-local',
    gulp.series(gulp.parallel('certificate', 'dbBuild', 'buildsite'),
        'terminate'));


gulp.task('rebuild:website',
    gulp.series('stopService', 'clean:website', 'build:website', 'start:site')
);