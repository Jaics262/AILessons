const childProcess = require('child_process');
const dartsass = require('gulp-sass')(require('sass'));
const postcss = require('gulp-postcss');
const autoprefixer = require('autoprefixer');
const sassLint = require('gulp-sass-lint');
const rename = require('gulp-rename');
const plumber = require('gulp-plumber');
const sourcemaps = require('gulp-sourcemaps');
const cssValidator = require('w3c-css-validator');
const gulp = require('gulp');
const browserSync = require('browser-sync');

const scssSources = [
    'swe.website/UI/assets/src/scss/**/*.s+(a|c)ss',
    '!swe.website/UI/assets/src/scss/forms-scss/**/*.s+(a|c)ss'
];

const projectFile = 'swe.website/swe.website.csproj';

dartsass.compiler.logger = {
    warn(message, options) {
        console.warn('SASS WARNING:', message);
        if (options && options.deprecation) {
            throw new Error(message);
        }
    }
};

function lintScss(configFile) {
    return gulp.src(scssSources)
        .pipe(sassLint({
            configFile: configFile,
            options: {
                formatter: 'stylish'
            }
        }))
        .pipe(sassLint.format())
        .pipe(sassLint.failOnError())
        .pipe(plumber());
}

// Publishes swe.website.csproj. Compression of the backoffice static files is
// skipped here so a clean publish does not brotli-compress every asset.
function buildWebsite(done) {
    let finished = false;
    const finish = (err) => {
        if (finished) {
            return;
        }
        finished = true;
        done(err);
    };

    const proc = childProcess.spawn('dotnet', [
        'publish',
        projectFile,
        '-o', 'swe.website/bin',
        '-p:Configuration=Website',
        '-p:CompressionEnabled=false'
    ], {
        cwd: process.cwd()
    });

    proc.stdout.on('data', (data) => {
        const text = data.toString();
        console.log(text);
        if (/SCS\d{4}/.test(text)) {
            finish(new Error('Security warning building project'));
        }
    });

    proc.stderr.on('data', (data) => {
        console.log(data.toString());
    });

    proc.on('error', finish);

    proc.on('close', (code) => {
        if (code !== 0) {
            finish(new Error('Error building project'));
            return;
        }
        finish();
    });
}

async function sassTask() {
    const rev = (await import('gulp-rev')).default;
    const filter = (await import('gulp-filter')).default;
    const cssnano = require('cssnano');
    const processors = [
        autoprefixer(),
        cssnano({
            preset: ['default', {
                normalizeWhitespace: true,
                discardComments: {
                    removeAll: true
                }
            }]
        })
    ];

    return gulp.src('swe.website/UI/assets/src/scss/*.scss')
        .pipe(plumber())
        .pipe(sourcemaps.init())
        .pipe(dartsass({
            outputStyle: 'compressed'
        }).on('error', dartsass.logError))
        .pipe(postcss(processors))
        .pipe(rename({ suffix: '.min' }))
        .pipe(sourcemaps.write('./maps/', {
            sourceRoot: '../../src/scss'
        }))
        .pipe(gulp.dest('swe.website/UI/assets/dist/css/'))
        .pipe(filter('**/*.css'))
        .pipe(rev())
        .pipe(gulp.dest('swe.website/UI/assets/dist/css/'))
        .pipe(rev.manifest('../css/rev-manifest.json'))
        .pipe(gulp.dest('swe.website/UI/assets/dist/css/'))
        .pipe(browserSync.reload({ stream: true, match: '**/*.css' }));
}

gulp.task('build:website', buildWebsite);
gulp.task('sass', sassTask);
gulp.task('lintsass', function () {
    return lintScss('sass-lint.yml');
});
gulp.task('lintsass:build', function () {
    return lintScss('sass-lint-final.yml');
});
gulp.task('compile:scss', function () {
    return gulp.src(scssSources)
        .pipe(dartsass().on('error', dartsass.logError))
        .pipe(gulp.dest('swe.website/UI/assets/dist/css/'));
});

gulp.task('css:w3c', () => {
    const globby = [
        'swe.website/UI/assets/dist/css/**/*.css',
        '!swe.website/UI/assets/dist/css/**/*.min.css'
    ];
    const failOnWarnings = process.argv.includes('--fail-on-warnings');
    const path = require('path');
    const through = require('through2').obj;
    const gutil = require('gulp-util');
    let totalFiles = 0;
    let totalErrors = 0;
    let totalWarnings = 0;

    return gulp.src(globby)
        .pipe(through(async function (file, enc, cb) {
            totalFiles += 1;
            const css = file.contents.toString('utf8');

            try {
                const result = await cssValidator.validateText(css, {
                    profile: 'css3svg',
                    warning: 2,
                    usermedium: 'all'
                });
                const errors = result.errors || [];
                const warnings = result.warnings || [];

                totalErrors += errors.length;
                totalWarnings += warnings.length;

                const rel = path.relative(process.cwd(), file.path);
                if (errors.length === 0 && warnings.length === 0) {
                    console.log(gutil.colors.green('✓ ' + rel + ' — no issues'));
                } else {
                    console.log(gutil.colors.bold('\n' + rel));
                    errors.forEach(e => {
                        const loc = e.line ? ':' + e.line : '';
                        console.log(gutil.colors.red('  ✖ ERROR' + loc + ' — ' + e.message));
                    });
                    warnings.forEach(w => {
                        const loc = w.line ? ':' + w.line : '';
                        console.log(gutil.colors.yellow('  ⚠ WARNING' + loc + ' — ' + w.message));
                    });
                }

                cb(null, file);
            } catch (err) {
                console.error(gutil.colors.red('[css:w3c] Request failed for ' + file.path));
                console.error(err && err.message ? err.message : err);
                totalErrors += 1;
                cb(null, file);
            }
        }, function flush(cb) {
            const summary = '\nW3C CSS validation summary: ' + totalFiles + ' file(s), '
                + totalErrors + ' error(s), ' + totalWarnings + ' warning(s)';
            console.log(totalErrors ? gutil.colors.red(summary) : gutil.colors.green(summary));

            if (totalErrors > 0 || (failOnWarnings && totalWarnings > 0)) {
                cb(new Error('CSS validation failed.'));
            } else {
                cb();
            }
        }));
});

gulp.task('sassTasks', gulp.series('sass', 'lintsass'));
gulp.task('sassTasks:build', gulp.series('sass', 'lintsass:build'));

exports.sass = sassTask;
exports.buildWebsite = buildWebsite;
